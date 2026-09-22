using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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
using Microsoft.AspNetCore.WebUtilities;
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
    options.AddPolicy("CanManageUsers", policy => policy.RequireRole(ApplicationRoles.Owner));
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
builder.Services.AddHttpClient<EmailSender>();
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
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Security token validation failed.", detail = exception.Message });
            return;
        }
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

api.MapPost("/auth/change-password", async (
    ChangePasswordRequest request,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ClaimsPrincipal principal) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    user.MustChangePassword = false;
    await userManager.UpdateAsync(user);
    await signInManager.RefreshSignInAsync(user);
    return Results.Ok(await BuildUserResponseAsync(userManager, principal));
}).RequireAuthorization();

api.MapPost("/auth/forgot-password", async (
    ForgotPasswordRequest request,
    UserManager<ApplicationUser> userManager,
    EmailSender emailSender) =>
{
    var user = await userManager.FindByEmailAsync(request.Email.Trim());
    if (user is not null)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailSender.SendPasswordResetAsync(user.Email!, user.DisplayName, token);
    }

    return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
});

api.MapPost("/auth/reset-password", async (
    ResetPasswordRequest request,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(request.Email.Trim());
    if (user is null)
    {
        return Results.BadRequest(new { error = "Invalid password reset request." });
    }

    string token;
    try
    {
        token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
    }
    catch (FormatException)
    {
        token = request.Token;
    }

    var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    user.MustChangePassword = false;
    user.EmailConfirmed = true;
    await userManager.UpdateAsync(user);
    return Results.Ok(new { message = "Password reset completed." });
});

api.MapPost("/auth/invitations", async (
    InviteUserRequest request,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    EmailSender emailSender,
    ClaimsPrincipal principal) =>
{
    await EnsureRolesAsync(roleManager);

    var role = string.Equals(request.Role, ApplicationRoles.Editor, StringComparison.OrdinalIgnoreCase)
        ? ApplicationRoles.Editor
        : ApplicationRoles.Viewer;
    var email = request.Email.Trim();
    var user = await userManager.FindByEmailAsync(email);
    var temporaryPassword = GenerateTemporaryPassword();

    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? email.Split('@')[0]
                : request.DisplayName.Trim(),
            MustChangePassword = true
        };

        var createResult = await userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
        {
            return Results.BadRequest(new { errors = createResult.Errors.Select(error => error.Description) });
        }
    }
    else
    {
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
        if (!resetResult.Succeeded)
        {
            return Results.BadRequest(new { errors = resetResult.Errors.Select(error => error.Description) });
        }

        user.MustChangePassword = true;
        user.EmailConfirmed = true;
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        await userManager.UpdateAsync(user);
    }

    if (!await userManager.IsInRoleAsync(user, role))
    {
        await userManager.AddToRoleAsync(user, role);
    }

    if (role == ApplicationRoles.Editor && await userManager.IsInRoleAsync(user, ApplicationRoles.Viewer))
    {
        await userManager.RemoveFromRoleAsync(user, ApplicationRoles.Viewer);
    }

    await emailSender.SendInvitationAsync(user.Email!, user.DisplayName, role, temporaryPassword, principal.Identity?.Name);
    return Results.Ok(new { email = user.Email, user.DisplayName, role });
}).RequireAuthorization("CanManageUsers");

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

api.MapDelete("/albums/{albumId:guid}/photos/{photoId:guid}/assignment", async (
    Guid albumId,
    Guid photoId,
    IAlbumRepository albums,
    CancellationToken cancellationToken) =>
{
    try
    {
        await albums.UnassignPhotoAsync(albumId, photoId, cancellationToken);
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
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
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
    catch (Exception exception)
    {
        return Results.Problem(
            detail: environment.IsDevelopment() ? exception.ToString() : exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Photo upload failed.");
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
    return new { email = user.Email, user.DisplayName, roles, mustChangePassword = user.MustChangePassword };
}

static string GenerateTemporaryPassword()
{
    const string lower = "abcdefghijkmnopqrstuvwxyz";
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const string digits = "23456789";
    const string all = lower + upper + digits;
    Span<char> password = stackalloc char[14];
    password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
    password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
    password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
    for (var index = 3; index < password.Length; index++)
    {
        password[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
    }

    RandomNumberGenerator.Shuffle(password);
    return new string(password);
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

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record ForgotPasswordRequest(string Email);

internal sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

internal sealed record InviteUserRequest(string Email, string Role, string? DisplayName);

internal sealed record UpdatePageLayoutRequest(string Layout);

internal sealed record AddPageRequest(string? Layout);

internal sealed record AssignPhotoRequest(int SortOrder);

internal sealed record UpdatePhotoRequest(string? Alt, string? Caption);

internal sealed class EmailSender(HttpClient httpClient, IConfiguration configuration)
{
    private const string ResendEndpoint = "https://api.resend.com/emails";

    public Task SendInvitationAsync(string to, string displayName, string role, string temporaryPassword, string? invitedBy)
    {
        var loginUrl = BuildPublicUrl("/?studio=1");
        var safeName = HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(displayName) ? to : displayName);
        var safeInviter = HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(invitedBy) ? "Pablo's Album" : invitedBy);
        var safeRole = HtmlEncoder.Default.Encode(role);
        var safePassword = HtmlEncoder.Default.Encode(temporaryPassword);
        var safeLoginUrl = HtmlEncoder.Default.Encode(loginUrl);

        return SendAsync(
            to,
            "Invitacion a Pablo's Album Studio",
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.5;color:#2b2520">
              <h1 style="font-family:Georgia,serif">Pablo's Album Studio</h1>
              <p>Hola {safeName},</p>
              <p>{safeInviter} te invito a colaborar como <strong>{safeRole}</strong>.</p>
              <p>Tu password temporal es:</p>
              <p style="font-size:20px;font-weight:700;background:#f4ead8;padding:12px;border-radius:6px">{safePassword}</p>
              <p>Entra al Studio y el sistema te pedira cambiarlo antes de continuar.</p>
              <p><a href="{safeLoginUrl}" style="background:#2f3f4c;color:#fffaf0;padding:12px 18px;text-decoration:none;border-radius:6px">Abrir Studio</a></p>
              <p>Si no esperabas esta invitacion, puedes ignorar este correo.</p>
            </div>
            """);
    }

    public Task SendPasswordResetAsync(string to, string displayName, string token)
    {
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = BuildPublicUrl($"/?reset=1&email={Uri.EscapeDataString(to)}&token={Uri.EscapeDataString(encodedToken)}");
        var safeName = HtmlEncoder.Default.Encode(string.IsNullOrWhiteSpace(displayName) ? to : displayName);
        var safeResetUrl = HtmlEncoder.Default.Encode(resetUrl);

        return SendAsync(
            to,
            "Reinicia tu password de Pablo's Album",
            $"""
            <div style="font-family:Arial,sans-serif;line-height:1.5;color:#2b2520">
              <h1 style="font-family:Georgia,serif">Reinicio de password</h1>
              <p>Hola {safeName},</p>
              <p>Recibimos una solicitud para cambiar tu password de Pablo's Album Studio.</p>
              <p><a href="{safeResetUrl}" style="background:#2f3f4c;color:#fffaf0;padding:12px 18px;text-decoration:none;border-radius:6px">Cambiar password</a></p>
              <p>Si no fuiste tu, ignora este correo.</p>
            </div>
            """);
    }

    private async Task SendAsync(string to, string subject, string html)
    {
        var provider = configuration["Email:Provider"] ?? Environment.GetEnvironmentVariable("EMAIL_PROVIDER") ?? "Resend";
        if (!string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email provider is not configured for Resend.");
        }

        var apiKey = configuration["Resend:ApiKey"] ?? Environment.GetEnvironmentVariable("RESEND_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("RESEND_API_KEY is not configured.");
        }

        var from = configuration["Email:From"] ?? Environment.GetEnvironmentVariable("EMAIL_FROM") ?? "Pablo's Album <no-reply@pablosalbum.com>";
        var replyTo = configuration["Email:ReplyTo"] ?? Environment.GetEnvironmentVariable("EMAIL_REPLY_TO");
        using var request = new HttpRequestMessage(HttpMethod.Post, ResendEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        var payload = new Dictionary<string, object?>
        {
            ["from"] = from,
            ["to"] = new[] { to },
            ["subject"] = subject,
            ["html"] = html
        };
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            payload["reply_to"] = replyTo;
        }

        request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Resend email failed with {(int)response.StatusCode}: {body}");
        }
    }

    private string BuildPublicUrl(string pathAndQuery)
    {
        var baseUrl = configuration["App:PublicUrl"] ?? Environment.GetEnvironmentVariable("APP_PUBLIC_URL") ?? "https://pablosalbum.com";
        return $"{baseUrl.TrimEnd('/')}{pathAndQuery}";
    }
}
