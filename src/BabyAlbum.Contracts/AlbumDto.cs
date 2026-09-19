namespace BabyAlbum.Contracts;

public sealed record AlbumDto(
    Guid Id,
    string Title,
    string Subtitle,
    string Description,
    string Theme,
    string CoverPhotoUrl,
    IReadOnlyList<AlbumPageDto> Pages,
    IReadOnlyList<MemoryDto> Memories);

public sealed record AlbumPageDto(
    Guid Id,
    int PageNumber,
    string Layout,
    string Title,
    string DateLabel,
    string Text,
    IReadOnlyList<PhotoDto> Photos);

public sealed record PhotoDto(
    Guid Id,
    string Url,
    string Alt,
    string Caption,
    string StorageProvider,
    string StorageKey,
    int SortOrder);

public sealed record PhotoLibraryItemDto(
    Guid Id,
    Guid PageId,
    int PageNumber,
    string Url,
    string Alt,
    string Caption,
    string StorageProvider,
    string StorageKey,
    int SortOrder);

public sealed record MemoryDto(
    Guid Id,
    string Title,
    string Description,
    DateOnly Date,
    string Type,
    Guid? LinkedPageId);
