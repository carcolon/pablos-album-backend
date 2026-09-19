namespace BabyAlbum.Domain.Albums;

public sealed class Album
{
    public Album(
        Guid id,
        string title,
        string subtitle,
        string description,
        string theme,
        string coverPhotoUrl,
        IReadOnlyList<AlbumPage> pages,
        IReadOnlyList<Memory> memories)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Album title is required.", nameof(title));
        }

        Id = id;
        Title = title;
        Subtitle = subtitle;
        Description = description;
        Theme = theme;
        CoverPhotoUrl = coverPhotoUrl;
        Pages = pages.OrderBy(page => page.PageNumber).ToArray();
        Memories = memories.OrderBy(memory => memory.Date).ToArray();
    }

    public Guid Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Description { get; }

    public string Theme { get; }

    public string CoverPhotoUrl { get; }

    public IReadOnlyList<AlbumPage> Pages { get; }

    public IReadOnlyList<Memory> Memories { get; }
}
