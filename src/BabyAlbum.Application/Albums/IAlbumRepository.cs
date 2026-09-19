using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Albums;

public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> ListVisibleAsync(CancellationToken cancellationToken);

    Task<Album?> GetAsync(Guid albumId, CancellationToken cancellationToken);
}
