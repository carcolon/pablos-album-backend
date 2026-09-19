using BabyAlbum.Application.Media;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace BabyAlbum.Infrastructure.Storage.GoogleDrive;

public sealed class GoogleDriveMediaStorage : IMediaStorage
{
    private static readonly string[] Scopes = [DriveService.Scope.DriveFile];
    private readonly GoogleDriveOptions _options;

    public GoogleDriveMediaStorage(IOptions<GoogleDriveOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredMedia> UploadAsync(MediaUpload upload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FolderId))
        {
            throw new InvalidOperationException("Google Drive folder is not configured. Set GOOGLE_DRIVE_FOLDER_ID in Render.");
        }

        using var drive = CreateDriveService();
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = upload.FileName,
            Parents = [_options.FolderId]
        };

        var request = drive.Files.Create(metadata, upload.Content, upload.ContentType);
        request.Fields = "id,name,size,mimeType";

        var progress = await request.UploadAsync(cancellationToken);
        if (progress.Exception is not null)
        {
            throw new InvalidOperationException("Google Drive upload failed.", progress.Exception);
        }

        var file = request.ResponseBody;
        return new StoredMedia(
            "GOOGLE_DRIVE",
            file.Id,
            file.Name,
            file.MimeType ?? upload.ContentType,
            (long?)file.Size ?? upload.Content.Length);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var drive = CreateDriveService();
        var output = new MemoryStream();
        await drive.Files.Get(storageKey).DownloadAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        using var drive = CreateDriveService();
        await drive.Files.Delete(storageKey).ExecuteAsync(cancellationToken);
    }

    private DriveService CreateDriveService()
    {
        IConfigurableHttpClientInitializer credential;

        if (HasOAuthCredentials())
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _options.OAuthClientId,
                    ClientSecret = _options.OAuthClientSecret
                },
                Scopes = Scopes
            });

            credential = new UserCredential(
                flow,
                _options.OAuthUser,
                new TokenResponse { RefreshToken = _options.OAuthRefreshToken });
        }
        else if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
        {
            credential = CredentialFactory
                .FromJson(_options.ServiceAccountJson, JsonCredentialParameters.ServiceAccountCredentialType)
                .CreateScoped(Scopes);
        }
        else if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            credential = CredentialFactory
                .FromFile(_options.ServiceAccountJsonPath, JsonCredentialParameters.ServiceAccountCredentialType)
                .CreateScoped(Scopes);
        }
        else
        {
            throw new InvalidOperationException("Google Drive credentials are not configured. Set OAuth refresh-token credentials for a personal Drive, or service-account credentials for a Shared Drive.");
        }

        return new DriveService(new BaseClientService.Initializer
        {
            ApplicationName = _options.ApplicationName,
            HttpClientInitializer = credential
        });
    }

    private bool HasOAuthCredentials()
    {
        return !string.IsNullOrWhiteSpace(_options.OAuthClientId)
            && !string.IsNullOrWhiteSpace(_options.OAuthClientSecret)
            && !string.IsNullOrWhiteSpace(_options.OAuthRefreshToken);
    }
}
