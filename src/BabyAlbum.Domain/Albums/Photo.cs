namespace BabyAlbum.Domain.Albums;

public sealed record Photo(
    Guid Id,
    string Url,
    string Alt,
    string Caption,
    string StorageProvider,
    string StorageKey,
    int SortOrder);
