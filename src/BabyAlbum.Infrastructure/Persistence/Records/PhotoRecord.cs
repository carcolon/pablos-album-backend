using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Infrastructure.Persistence.Records;

public sealed class PhotoRecord
{
    public Guid Id { get; set; }

    public Guid AlbumId { get; set; }

    public Guid? AlbumPageId { get; set; }

    public AlbumPageRecord? AlbumPage { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Alt { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StorageProvider { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Photo ToDomain()
    {
        return new Photo(Id, Url, Alt, Caption, StorageProvider, StorageKey, SortOrder);
    }
}
