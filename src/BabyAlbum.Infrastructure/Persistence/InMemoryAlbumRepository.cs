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
            "A premium album viewer and admin studio prototype built from the technical guide.",
            "classic-warm",
            "https://images.unsplash.com/photo-1519689680058-324335c77eba?auto=format&fit=crop&w=1800&q=85",
            [
                new AlbumPage(
                    page1,
                    1,
                    LayoutType.FullPhoto,
                    "Before You",
                    "Chapter 00",
                    "A quiet page for the little rituals, notes and photographs that made room for Pablo before the first hello.",
                    [
                        new Photo(
                            Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98d001"),
                            "https://images.unsplash.com/photo-1491013516836-7db643ee125a?auto=format&fit=crop&w=1600&q=85",
                            "A warm family moment near a window.",
                            "Waiting for you with a house already full of stories.",
                            "SEED",
                            "before-you-cover",
                            1)
                    ]),
                new AlbumPage(
                    page2,
                    2,
                    LayoutType.PhotoWithCaption,
                    "Hello World",
                    "The first chapter",
                    "The album opens with a first portrait, a date, and space for the words everyone will want to read again years from now.",
                    [
                        new Photo(
                            Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98d002"),
                            "https://images.unsplash.com/photo-1522771930-78848d9293e8?auto=format&fit=crop&w=1600&q=85",
                            "A baby resting peacefully.",
                            "The first hello.",
                            "SEED",
                            "hello-world",
                            1)
                    ]),
                new AlbumPage(
                    page3,
                    3,
                    LayoutType.TwoPhotos,
                    "Small Discoveries",
                    "First month",
                    "Two-photo spreads make room for comparisons: tiny hands, sleepy mornings, and the details that change faster than anyone expects.",
                    [
                        new Photo(
                            Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98d003"),
                            "https://images.unsplash.com/photo-1546015720-b8b30df5aa27?auto=format&fit=crop&w=1200&q=85",
                            "A close family detail.",
                            "Tiny hands.",
                            "SEED",
                            "small-discoveries-1",
                            1),
                        new Photo(
                            Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98d004"),
                            "https://images.unsplash.com/photo-1515488042361-ee00e0ddd4e4?auto=format&fit=crop&w=1200&q=85",
                            "Soft toys in a nursery.",
                            "A room becoming his.",
                            "SEED",
                            "small-discoveries-2",
                            2)
                    ]),
                new AlbumPage(
                    page4,
                    4,
                    LayoutType.Letter,
                    "A Letter for Later",
                    "Read this when you are older",
                    "Pablo, this page is for the words that do not fit under a photograph. The app treats letters as first-class memories so the family can preserve voice, context and tenderness, not only images.",
                    [])
            ],
            [
                new Memory(Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98e001"), "Album started", "The first private prototype is ready to grow into the real family album.", new DateOnly(2026, 9, 18), "Milestone", page1),
                new Memory(Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98e002"), "Viewer experience", "Page flip, editorial spreads and responsive reading are part of the first usable slice.", new DateOnly(2026, 9, 18), "Experience", page2),
                new Memory(Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98e003"), "Admin Studio", "Layouts, pages, invitations and audit notes are visible for the owner workflow.", new DateOnly(2026, 9, 18), "Admin", page3)
            ]);
    }
}
