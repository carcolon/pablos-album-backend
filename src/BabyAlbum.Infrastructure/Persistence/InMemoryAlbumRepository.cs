using BabyAlbum.Application.Albums;
using BabyAlbum.Domain.Albums;

namespace BabyAlbum.Infrastructure.Persistence;

public sealed class InMemoryAlbumRepository : IAlbumRepository
{
    private static readonly Guid AlbumId = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c041");
    private readonly Album[] _albums = [CreatePabloAlbum()];

    public Task<IReadOnlyList<Album>> ListVisibleAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Album>>(_albums);
    }

    public Task<Album?> GetAsync(Guid albumId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_albums.FirstOrDefault(album => album.Id == albumId));
    }

    private static Album CreatePabloAlbum()
    {
        var page1 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c101");
        var page2 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c102");
        var page3 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c103");
        var page4 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c104");

        return new Album(
            AlbumId,
            "Pablo's Album",
            "A private family book for the moments that become home.",
            "A family album ready for Pablo's real photos, notes and milestones.",
            "classic-warm",
            string.Empty,
            [
                new AlbumPage(
                    page1,
                    1,
                    LayoutType.FullPhoto,
                    "Page One",
                    "Family archive",
                    "Choose a layout in Studio and add Pablo's real memories here.",
                    []),
                new AlbumPage(
                    page2,
                    2,
                    LayoutType.PhotoWithCaption,
                    "Page Two",
                    "Family archive",
                    "This page is ready for a photo, caption and date.",
                    []),
                new AlbumPage(
                    page3,
                    3,
                    LayoutType.TwoPhotos,
                    "Page Three",
                    "Family archive",
                    "Use a two-photo spread for before/after moments, details or comparisons.",
                    []),
                new AlbumPage(
                    page4,
                    4,
                    LayoutType.Letter,
                    "Letter Page",
                    "Read this when you are older",
                    "Write a family note here when the album content is ready.",
                    [])
            ],
            [
                new Memory(Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98e001"), "Album created", "The album structure is ready for real family content.", new DateOnly(2026, 9, 18), "Milestone", page1)
            ]);
    }
}
