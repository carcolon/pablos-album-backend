namespace BabyAlbum.Domain.Albums;

public sealed record Memory(
    Guid Id,
    string Title,
    string Description,
    DateOnly Date,
    string Type,
    Guid? LinkedPageId);
