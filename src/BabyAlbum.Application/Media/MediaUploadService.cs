using BabyAlbum.Application.Albums;
using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Media;

public sealed class MediaUploadService
{
    public const long MaxStoredImageSizeBytes = 10 * 1024 * 1024;
    private const long MaxIncomingImageSizeBytes = 50 * 1024 * 1024;

    private readonly IAlbumRepository _albums;
    private readonly IImageProcessor _imageProcessor;
    private readonly IMediaStorage _mediaStorage;

    public MediaUploadService(
        IAlbumRepository albums,
        IImageProcessor imageProcessor,
        IMediaStorage mediaStorage)
    {
        _albums = albums;
        _imageProcessor = imageProcessor;
        _mediaStorage = mediaStorage;
    }

    public async Task<UploadPhotoResult> UploadPhotoAsync(
        Guid albumId,
        Guid pageId,
        IncomingImage image,
        CancellationToken cancellationToken)
    {
        var album = await _albums.GetAsync(albumId, cancellationToken);
        if (album is null)
        {
            throw new AlbumNotFoundException(albumId);
        }

        if (image.SizeBytes <= 0)
        {
            throw new InvalidImageException("The uploaded file is empty.");
        }

        if (image.SizeBytes > MaxIncomingImageSizeBytes)
        {
            throw new InvalidImageException("The uploaded file is larger than the 50 MB intake limit.");
        }

        await using var processed = await _imageProcessor.PrepareForStorageAsync(
            image,
            MaxStoredImageSizeBytes,
            cancellationToken);

        var stored = await _mediaStorage.UploadAsync(
            new MediaUpload(processed.FileName, processed.ContentType, processed.Content),
            cancellationToken);

        var photoId = Guid.NewGuid();
        var photo = new Photo(
            photoId,
            $"/api/photos/{photoId}/content",
            Path.GetFileNameWithoutExtension(image.FileName),
            string.Empty,
            stored.StorageProvider,
            stored.StorageKey,
            0);

        try
        {
            await _albums.AddPhotoAsync(albumId, pageId, photo, stored.MimeType, cancellationToken);
        }
        catch
        {
            await _mediaStorage.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }

        return new UploadPhotoResult(
            albumId,
            pageId,
            photoId,
            image.FileName,
            processed.FileName,
            processed.ContentType,
            image.SizeBytes,
            stored.SizeBytes,
            processed.WasCompressed,
            stored.StorageProvider,
            stored.StorageKey);
    }
}

public sealed class AlbumNotFoundException : Exception
{
    public AlbumNotFoundException(Guid albumId)
        : base($"Album '{albumId}' was not found.")
    {
    }
}

public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string message)
        : base(message)
    {
    }
}
