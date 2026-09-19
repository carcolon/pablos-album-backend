using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Infrastructure.Persistence.Records;

public sealed class AlbumRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Theme { get; set; } = string.Empty;

    public string CoverPhotoUrl { get; set; } = string.Empty;

    public List<AlbumPageRecord> Pages { get; set; } = [];

    public List<MemoryRecord> Memories { get; set; } = [];

    public Album ToDomain()
    {
        return new Album(
            Id,
            Title,
            Subtitle,
            Description,
            Theme,
            CoverPhotoUrl,
            Pages.Select(page => page.ToDomain()).ToArray(),
            Memories.Select(memory => memory.ToDomain()).ToArray());
    }
}
