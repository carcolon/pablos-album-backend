using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Albums;

public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> ListVisibleAsync(CancellationToken cancellationToken);

    Task<Album?> GetAsync(Guid albumId, CancellationToken cancellationToken);

    Task<Photo?> GetPhotoAsync(Guid photoId, CancellationToken cancellationToken);

    Task<string?> GetPhotoContentTypeAsync(Guid photoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlbumPhoto>> ListPhotosAsync(Guid albumId, CancellationToken cancellationToken);

    Task UpdateAlbumCoverTextAsync(Guid albumId, string title, string subtitle, string description, CancellationToken cancellationToken);

    Task UpdatePageLayoutAsync(Guid albumId, Guid pageId, LayoutType layout, CancellationToken cancellationToken);

    Task<AlbumPage> AddPageAsync(Guid albumId, LayoutType layout, CancellationToken cancellationToken);

    Task DeletePageAsync(Guid albumId, Guid pageId, CancellationToken cancellationToken);

    Task AddPhotoAsync(Guid albumId, Guid pageId, Photo photo, string contentType, CancellationToken cancellationToken);

    Task AssignPhotoToPageAsync(Guid albumId, Guid pageId, Guid photoId, int sortOrder, CancellationToken cancellationToken);

    Task UnassignPhotoAsync(Guid albumId, Guid photoId, CancellationToken cancellationToken);

    Task UpdatePhotoAsync(Guid photoId, string alt, string caption, CancellationToken cancellationToken);
}
