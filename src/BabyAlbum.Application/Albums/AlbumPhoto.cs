using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Albums;

public sealed record AlbumPhoto(
    Guid Id,
    Guid? PageId,
    int? PageNumber,
    string Url,
    string Alt,
    string Caption,
    string StorageProvider,
    string StorageKey,
    int SortOrder)
{
    public Photo ToPhoto()
    {
        return new Photo(Id, Url, Alt, Caption, StorageProvider, StorageKey, SortOrder);
    }
}
