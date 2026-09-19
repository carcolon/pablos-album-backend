using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Infrastructure.Persistence.Records;

public sealed class AlbumPageRecord
{
    public Guid Id { get; set; }

    public Guid AlbumId { get; set; }

    public AlbumRecord? Album { get; set; }

    public int PageNumber { get; set; }

    public LayoutType Layout { get; set; }

    public string Title { get; set; } = string.Empty;

    public string DateLabel { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public List<PhotoRecord> Photos { get; set; } = [];

    public AlbumPage ToDomain()
    {
        return new AlbumPage(
            Id,
            PageNumber,
            Layout,
            Title,
            DateLabel,
            Text,
            Photos.Select(photo => photo.ToDomain()).ToArray());
    }
}
