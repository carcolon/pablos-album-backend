using BabyAlbum.Application.Media;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
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
        GoogleCredential credential;

        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
        {
            credential = CredentialFactory
                .FromJson(_options.ServiceAccountJson, JsonCredentialParameters.ServiceAccountCredentialType);
        }
        else if (!string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath))
        {
            credential = CredentialFactory
                .FromFile(_options.ServiceAccountJsonPath, JsonCredentialParameters.ServiceAccountCredentialType);
        }
        else
        {
            throw new InvalidOperationException("Google Drive credentials are not configured. Set GOOGLE_DRIVE_SERVICE_ACCOUNT_JSON in Render.");
        }

        return new DriveService(new BaseClientService.Initializer
        {
            ApplicationName = _options.ApplicationName,
            HttpClientInitializer = credential.CreateScoped(Scopes)
        });
    }
}
