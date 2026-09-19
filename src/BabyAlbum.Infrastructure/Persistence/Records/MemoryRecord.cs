using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Infrastructure.Persistence.Records;

public sealed class MemoryRecord
{
    public Guid Id { get; set; }

    public Guid AlbumId { get; set; }

    public AlbumRecord? Album { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid? LinkedPageId { get; set; }

    public Memory ToDomain()
    {
        return new Memory(Id, Title, Description, Date, Type, LinkedPageId);
    }
}
