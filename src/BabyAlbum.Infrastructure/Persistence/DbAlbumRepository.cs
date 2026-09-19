using BabyAlbum.Application.Albums;
using BabyAlbum.Domain.Albums;
using BabyAlbum.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace BabyAlbum.Infrastructure.Persistence;

public sealed class DbAlbumRepository : IAlbumRepository
{
    private static readonly Guid AlbumId = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c041");
    private readonly AppDbContext _dbContext;

    public DbAlbumRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Album>> ListVisibleAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        var albums = await AlbumQuery()
            .OrderBy(album => album.Title)
            .ToListAsync(cancellationToken);

        return albums.Select(album => album.ToDomain()).ToArray();
    }

    public async Task<Album?> GetAsync(Guid albumId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        var album = await AlbumQuery()
            .FirstOrDefaultAsync(album => album.Id == albumId, cancellationToken);

        return album?.ToDomain();
    }

    public async Task<Photo?> GetPhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await _dbContext.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(photo => photo.Id == photoId, cancellationToken);

        return photo?.ToDomain();
    }

    public async Task<string?> GetPhotoContentTypeAsync(Guid photoId, CancellationToken cancellationToken)
    {
        return await _dbContext.Photos
            .AsNoTracking()
            .Where(photo => photo.Id == photoId)
            .Select(photo => photo.ContentType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdatePageLayoutAsync(Guid albumId, Guid pageId, LayoutType layout, CancellationToken cancellationToken)
    {
        var page = await _dbContext.AlbumPages
            .FirstOrDefaultAsync(page => page.Id == pageId && page.AlbumId == albumId, cancellationToken);

        if (page is null)
        {
            throw new InvalidOperationException("Album page was not found.");
        }

        page.Layout = layout;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPhotoAsync(Guid albumId, Guid pageId, Photo photo, string contentType, CancellationToken cancellationToken)
    {
        var page = await _dbContext.AlbumPages
            .Include(page => page.Photos)
            .FirstOrDefaultAsync(page => page.Id == pageId && page.AlbumId == albumId, cancellationToken);

        if (page is null)
        {
            throw new InvalidOperationException("Album page was not found.");
        }

        page.Photos.Add(new PhotoRecord
        {
            Id = photo.Id,
            AlbumPageId = page.Id,
            Url = photo.Url,
            Alt = photo.Alt,
            Caption = photo.Caption,
            ContentType = contentType,
            StorageProvider = photo.StorageProvider,
            StorageKey = photo.StorageKey,
            SortOrder = page.Photos.Count + 1
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AlbumRecord> AlbumQuery()
    {
        return _dbContext.Albums
            .AsNoTracking()
            .Include(album => album.Pages.OrderBy(page => page.PageNumber))
            .ThenInclude(page => page.Photos.OrderBy(photo => photo.SortOrder))
            .Include(album => album.Memories.OrderBy(memory => memory.Date));
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Albums.AnyAsync(cancellationToken))
        {
            return;
        }

        var page1 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c101");
        var page2 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c102");
        var page3 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c103");
        var page4 = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98c104");

        _dbContext.Albums.Add(new AlbumRecord
        {
            Id = AlbumId,
            Title = "Pablo's Album",
            Subtitle = "A private family book for the moments that become home.",
            Description = "A family album ready for Pablo's real photos, notes and milestones.",
            Theme = "classic-warm",
            CoverPhotoUrl = string.Empty,
            Pages =
            [
                new AlbumPageRecord
                {
                    Id = page1,
                    PageNumber = 1,
                    Layout = LayoutType.FullPhoto,
                    Title = "Page One",
                    DateLabel = "Family archive",
                    Text = "Choose a layout in Studio and add Pablo's real memories here."
                },
                new AlbumPageRecord
                {
                    Id = page2,
                    PageNumber = 2,
                    Layout = LayoutType.PhotoWithCaption,
                    Title = "Page Two",
                    DateLabel = "Family archive",
                    Text = "This page is ready for a photo, caption and date."
                },
                new AlbumPageRecord
                {
                    Id = page3,
                    PageNumber = 3,
                    Layout = LayoutType.TwoPhotos,
                    Title = "Page Three",
                    DateLabel = "Family archive",
                    Text = "Use a two-photo spread for before/after moments, details or comparisons."
                },
                new AlbumPageRecord
                {
                    Id = page4,
                    PageNumber = 4,
                    Layout = LayoutType.Letter,
                    Title = "Letter Page",
                    DateLabel = "Read this when you are older",
                    Text = "Write a family note here when the album content is ready."
                }
            ],
            Memories =
            [
                new MemoryRecord
                {
                    Id = Guid.Parse("018f4b44-6f15-7a45-a810-a1168d98e001"),
                    Title = "Album created",
                    Description = "The album structure is ready for real family content.",
                    Date = new DateOnly(2026, 9, 18),
                    Type = "Milestone",
                    LinkedPageId = page1
                }
            ]
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
