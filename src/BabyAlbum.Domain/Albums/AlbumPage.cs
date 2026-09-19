namespace BabyAlbum.Domain.Albums;

public sealed record AlbumPage(
    Guid Id,
    int PageNumber,
    LayoutType Layout,
    string Title,
    string DateLabel,
    string Text,
    IReadOnlyList<Photo> Photos);
