using BabyAlbum.Contracts;
using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Application.Albums;

public static class AlbumMapper
{
    public static AlbumDto ToDto(this Album album)
    {
        return new AlbumDto(
            album.Id,
            album.Title,
            album.Subtitle,
            album.Description,
            album.Theme,
            album.CoverPhotoUrl,
            album.Pages.Select(ToDto).ToArray(),
            album.Memories.Select(ToDto).ToArray());
    }

    private static AlbumPageDto ToDto(AlbumPage page)
    {
        return new AlbumPageDto(
            page.Id,
            page.PageNumber,
            page.Layout.ToString(),
            page.Title,
            page.DateLabel,
            page.Text,
            page.Photos.Select(ToDto).ToArray());
    }

    private static PhotoDto ToDto(Photo photo)
    {
        return new PhotoDto(
            photo.Id,
            photo.Url,
            photo.Alt,
            photo.Caption,
            photo.StorageProvider,
            photo.StorageKey,
            photo.SortOrder);
    }

    private static MemoryDto ToDto(Memory memory)
    {
        return new MemoryDto(
            memory.Id,
            memory.Title,
            memory.Description,
            memory.Date,
            memory.Type,
            memory.LinkedPageId);
    }
}
