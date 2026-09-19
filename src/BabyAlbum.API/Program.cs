using System.Security.Claims;
using BabyAlbum.Application.Albums;
using BabyAlbum.Application.Media;
using BabyAlbum.Contracts;
using BabyAlbum.Domain.Albums;
using BabyAlbum.Infrastructure;
using BabyAlbum.Infrastructure.Identity;
using BabyAlbum.Infrastructure.Persistence;
using BabyAlbum.Infrastructure.Storage.GoogleDrive;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
LoadDotEnvLocal(builder.Environment.ContentRootPath);

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
        var origins = new[]
            {
                "http://localhost:5173",
                "https://localhost:5173",
                "http://127.0.0.1:5173",
                "https://pablos-album-frontend.onrender.com"
            }
            .Concat(string.IsNullOrWhiteSpace(configuredOrigins)
                ? []
                : configuredOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(NormalizeOrigin)
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        policy
            .SetIsOriginAllowed(origin => origins.Contains(NormalizeOrigin(origin)))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "pablos_album_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "pablos_album_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/api/auth/unauthorized";
    options.AccessDeniedPath = "/api/auth/forbidden";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanEditAlbum", policy => policy.RequireRole(ApplicationRoles.Owner, ApplicationRoles.Editor));
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
    options.OAuthClientId ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_OAUTH_CLIENT_ID");
    options.OAuthClientSecret ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_OAUTH_CLIENT_SECRET");
    options.OAuthRefreshToken ??= Environment.GetEnvironmentVariable("GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN");
    options.OAuthUser = Environment.GetEnvironmentVariable("GOOGLE_DRIVE_OAUTH_USER") ?? options.OAuthUser;
});
builder.Services.AddScoped<AlbumReader>();
builder.Services.AddScoped<MediaUploadService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; script-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var isUnsafeApiRequest =
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method));

    if (isUnsafeApiRequest)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(context);
    }

    await next();
});

var api = app.MapGroup("/api");

api.MapGet("/security/csrf", (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    return Results.Ok(new { token = tokens.RequestToken });
});

api.MapGet("/auth/status", async (UserManager<ApplicationUser> userManager, ClaimsPrincipal principal) =>
{
    var owners = await userManager.GetUsersInRoleAsync(ApplicationRoles.Owner);
    return Results.Ok(new
    {
        hasOwner = owners.Count > 0,
        isAuthenticated = principal.Identity?.IsAuthenticated == true,
        user = principal.Identity?.IsAuthenticated == true ? await BuildUserResponseAsync(userManager, principal) : null
    });
});

api.MapGet("/auth/me", async (UserManager<ApplicationUser> userManager, ClaimsPrincipal principal) =>
{
    var response = await BuildUserResponseAsync(userManager, principal);
    return response is null ? Results.Unauthorized() : Results.Ok(response);
}).RequireAuthorization();

api.MapPost("/auth/register-owner", async (
    RegisterOwnerRequest request,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var owners = await userManager.GetUsersInRoleAsync(ApplicationRoles.Owner);
    if (owners.Count > 0)
    {
        return Results.Conflict(new { error = "Owner account already exists." });
    }

    await EnsureRolesAsync(roleManager);

    var user = new ApplicationUser
    {
        UserName = request.Email.Trim(),
        Email = request.Email.Trim(),
        DisplayName = request.DisplayName.Trim()
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    await userManager.AddToRoleAsync(user, ApplicationRoles.Owner);
    await signInManager.SignInAsync(user, isPersistent: true);
    return Results.Ok(new { email = user.Email, user.DisplayName, roles = new[] { ApplicationRoles.Owner } });
});

api.MapPost("/auth/login", async (
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var email = request.Email.Trim();
    var owners = await userManager.GetUsersInRoleAsync(ApplicationRoles.Owner);
    var user = await userManager.FindByEmailAsync(email);

    if (owners.Count == 0)
    {
        await EnsureRolesAsync(roleManager);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                    ? email.Split('@')[0]
                    : request.DisplayName.Trim()
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return Results.BadRequest(new { errors = createResult.Errors.Select(error => error.Description) });
            }
        }
        else
        {
            var passwordResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!passwordResult.Succeeded)
            {
                return Results.Unauthorized();
            }
        }

        if (!await userManager.IsInRoleAsync(user, ApplicationRoles.Owner))
        {
            await userManager.AddToRoleAsync(user, ApplicationRoles.Owner);
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Ok(new { email = user.Email, user.DisplayName, roles = new[] { ApplicationRoles.Owner } });
    }

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        return Results.Unauthorized();
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Ok(new { email = user.Email, user.DisplayName, roles });
});

api.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();

api.MapGet("/auth/unauthorized", () => Results.Unauthorized());
api.MapGet("/auth/forbidden", () => Results.Forbid());

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

api.MapPut("/albums/{albumId:guid}/pages/{pageId:guid}/layout", async (
    Guid albumId,
    Guid pageId,
    UpdatePageLayoutRequest request,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    if (!Enum.TryParse<LayoutType>(request.Layout, ignoreCase: true, out var layout))
    {
        return Results.BadRequest(new { error = "Unsupported page layout." });
    }

    try
    {
        await albums.UpdatePageLayoutAsync(albumId, pageId, layout, cancellationToken);
        return Results.NoContent();
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
}).RequireAuthorization("CanEditAlbum");

api.MapPost("/albums/{albumId:guid}/pages", async (
    Guid albumId,
    AddPageRequest request,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    var requestedLayout = string.IsNullOrWhiteSpace(request.Layout) ? LayoutType.FullPhoto.ToString() : request.Layout;
    if (!Enum.TryParse<LayoutType>(requestedLayout, ignoreCase: true, out var layout))
    {
        return Results.BadRequest(new { error = "Unsupported page layout." });
    }

    try
    {
        var page = await albums.AddPageAsync(albumId, layout, cancellationToken);
        return Results.Created($"/api/albums/{albumId}/pages/{page.Id}", new AlbumPageDto(
            page.Id,
            page.PageNumber,
            page.Layout.ToString(),
            page.Title,
            page.DateLabel,
            page.Text,
            page.Photos.Select(photo => new PhotoDto(
                photo.Id,
                photo.Url,
                photo.Alt,
                photo.Caption,
                photo.StorageProvider,
                photo.StorageKey,
                photo.SortOrder)).ToArray()));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (Exception exception)
    {
        return Results.Problem(
            detail: app.Environment.IsDevelopment() ? exception.ToString() : exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Photo upload failed.");
    }
}).RequireAuthorization("CanEditAlbum");

api.MapDelete("/albums/{albumId:guid}/pages/{pageId:guid}", async (
    Guid albumId,
    Guid pageId,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    try
    {
        await albums.DeletePageAsync(albumId, pageId, cancellationToken);
        return Results.NoContent();
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireAuthorization("CanEditAlbum");

api.MapGet("/albums/{albumId:guid}/photos", async (
    Guid albumId,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    var photos = await albums.ListPhotosAsync(albumId, cancellationToken);
    return Results.Ok(photos.Select(photo => new PhotoLibraryItemDto(
        photo.Id,
        photo.PageId,
        photo.PageNumber,
        photo.Url,
        photo.Alt,
        photo.Caption,
        photo.StorageProvider,
        photo.StorageKey,
        photo.SortOrder)));
}).RequireAuthorization("CanEditAlbum");

api.MapPut("/albums/{albumId:guid}/pages/{pageId:guid}/photos/{photoId:guid}", async (
    Guid albumId,
    Guid pageId,
    Guid photoId,
    AssignPhotoRequest request,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    try
    {
        await albums.AssignPhotoToPageAsync(albumId, pageId, photoId, request.SortOrder, cancellationToken);
        return Results.NoContent();
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireAuthorization("CanEditAlbum");

api.MapPost("/albums/{albumId:guid}/pages/{pageId:guid}/photos", async (
    Guid albumId,
    Guid pageId,
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
            pageId,
            new IncomingImage(file.FileName, file.ContentType, stream, file.Length),
            cancellationToken);

        return Results.Created($"/api/photos/{result.PhotoId}/content", result);
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
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireAuthorization("CanEditAlbum");

api.MapPut("/photos/{photoId:guid}", async (
    Guid photoId,
    UpdatePhotoRequest request,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    try
    {
        await albums.UpdatePhotoAsync(photoId, request.Alt ?? string.Empty, request.Caption ?? string.Empty, cancellationToken);
        return Results.NoContent();
    }
    catch (InvalidOperationException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
}).RequireAuthorization("CanEditAlbum");

api.MapGet("/photos/{photoId:guid}/content", async (
    Guid photoId,
    IAlbumRepository albums,
    IMediaStorage mediaStorage,
    CancellationToken cancellationToken) =>
{
    var photo = await albums.GetPhotoAsync(photoId, cancellationToken);
    if (photo is null)
    {
        return Results.NotFound();
    }

    var contentType = await albums.GetPhotoContentTypeAsync(photoId, cancellationToken) ?? "application/octet-stream";
    var stream = await mediaStorage.OpenReadAsync(photo.StorageKey, cancellationToken);
    return Results.File(stream, contentType);
});

api.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    application = "BabyAlbum",
    architecture = "Clean/Hexagonal MVP"
}));

app.Run();

static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
{
    foreach (var role in ApplicationRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

static async Task<object?> BuildUserResponseAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return null;
    }

    var roles = await userManager.GetRolesAsync(user);
    return new { email = user.Email, user.DisplayName, roles };
}

static void LoadDotEnvLocal(string contentRootPath)
{
    foreach (var directory in EnumerateCurrentAndParents(contentRootPath).Concat(EnumerateCurrentAndParents(Directory.GetCurrentDirectory())))
    {
        var path = Path.Combine(directory, ".env.local");
        if (!File.Exists(path))
        {
            continue;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');
            Environment.SetEnvironmentVariable(key, value);
        }

        return;
    }
}

static IEnumerable<string> EnumerateCurrentAndParents(string startPath)
{
    var directory = new DirectoryInfo(startPath);
    while (directory is not null)
    {
        yield return directory.FullName;
        directory = directory.Parent;
    }
}

static string NormalizeOrigin(string origin)
{
    return origin.Trim().TrimEnd('/');
}

internal sealed record RegisterOwnerRequest(string Email, string Password, string DisplayName);

internal sealed record LoginRequest(string Email, string Password, string? DisplayName);

internal sealed record UpdatePageLayoutRequest(string Layout);

internal sealed record AddPageRequest(string? Layout);

internal sealed record AssignPhotoRequest(int SortOrder);

internal sealed record UpdatePhotoRequest(string? Alt, string? Caption);
