namespace BabyAlbum.Application.Media;

public interface IMediaStorage
{
    Task<StoredMedia> UploadAsync(MediaUpload upload, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record MediaUpload(
    string FileName,
    string ContentType,
    Stream Content);

public sealed record StoredMedia(
    string StorageProvider,
    string StorageKey,
    string OriginalName,
    string MimeType,
    long SizeBytes);
