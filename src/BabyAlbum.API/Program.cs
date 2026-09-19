using BabyAlbum.Application.Albums;
using BabyAlbum.Application.Media;
using BabyAlbum.Infrastructure;
using BabyAlbum.Infrastructure.Storage.GoogleDrive;

var builder = WebApplication.CreateBuilder(args);

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"]
            ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        var origins = string.IsNullOrWhiteSpace(configuredOrigins)
            ? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173"]
            : configuredOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.Configure<GoogleDriveOptions>(options =>
{
    builder.Configuration.GetSection(GoogleDriveOptions.SectionName).Bind(options);
});
builder.Services.PostConfigure<GoogleDriveOptions>(options =>
{
    options.FolderId ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_FOLDER_ID");
    options.ServiceAccountJson ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON");
    options.ServiceAccountJsonPath ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON_PATH");
});
builder.Services.AddScoped<AlbumReader>();
builder.Services.AddScoped<MediaUploadService>();
builder.Services.AddInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();

var api = app.MapGroup("/api");

api.MapGet("/albums", async (AlbumReader reader, CancellationToken cancellationToken) =>
{
    var albums = await reader.ListVisibleAsync(cancellationToken);
    return Results.Ok(albums);
});

api.MapGet("/albums/{albumId:guid}", async (Guid albumId, AlbumReader reader, CancellationToken cancellationToken) =>
{
    var album = await reader.GetAsync(albumId, cancellationToken);
    return album is null ? Results.NotFound() : Results.Ok(album);
})
.WithName("GetAlbum");

api.MapPost("/albums/{albumId:guid}/photos", async (
    Guid albumId,
    HttpRequest request,
    MediaUploadService mediaUploadService,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Use multipart/form-data with a file field named 'file'." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];
    if (file is null)
    {
        return Results.BadRequest(new { error = "Missing file field." });
    }

    await using var stream = file.OpenReadStream();
    try
    {
        var result = await mediaUploadService.UploadPhotoAsync(
            albumId,
            new IncomingImage(file.FileName, file.ContentType, stream, file.Length),
            cancellationToken);

        return Results.Created($"/api/photos/{result.StorageKey}/content", result);
    }
    catch (AlbumNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (InvalidImageException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.DisableAntiforgery();

api.MapGet("/photos/{photoId:guid}/content", (Guid photoId) =>
{
    return Results.Problem(
        title: "Media proxy placeholder",
        detail: "The production version will authorize album access, resolve StorageProvider and StorageKey through IMediaStorage, and stream private media.",
        statusCode: StatusCodes.Status501NotImplemented);
});

api.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    application = "BabyAlbum",
    architecture = "Clean/Hexagonal MVP"
}));

app.Run();
