namespace BabyAlbum.Application.Media;

public sealed record UploadPhotoResult(
    Guid AlbumId,
    Guid PageId,
    Guid PhotoId,
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long OriginalSizeBytes,
    long StoredSizeBytes,
    bool WasCompressed,
    string StorageProvider,
    string StorageKey);
