using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Albums;

public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> ListVisibleAsync(CancellationToken cancellationToken);

    Task<Album?> GetAsync(Guid albumId, CancellationToken cancellationToken);

    Task<Photo?> GetPhotoAsync(Guid photoId, CancellationToken cancellationToken);

    Task<string?> GetPhotoContentTypeAsync(Guid photoId, CancellationToken cancellationToken);

    Task UpdatePageLayoutAsync(Guid albumId, Guid pageId, LayoutType layout, CancellationToken cancellationToken);

    Task AddPhotoAsync(Guid albumId, Guid pageId, Photo photo, string contentType, CancellationToken cancellationToken);
}
