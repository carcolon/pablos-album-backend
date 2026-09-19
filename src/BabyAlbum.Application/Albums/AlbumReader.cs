using BabyAlbum.Contracts;

namespace BabyAlbum.Application.Albums;

public sealed class AlbumReader
{
    private readonly IAlbumRepository _albums;

    public AlbumReader(IAlbumRepository albums)
    {
        _albums = albums;
    }

    public async Task<IReadOnlyList<AlbumDto>> ListVisibleAsync(CancellationToken cancellationToken)
    {
        var albums = await _albums.ListVisibleAsync(cancellationToken);
        return albums.Select(album => album.ToDto()).ToArray();
    }

    public async Task<AlbumDto?> GetAsync(Guid albumId, CancellationToken cancellationToken)
    {
        var album = await _albums.GetAsync(albumId, cancellationToken);
        return album?.ToDto();
    }
}
