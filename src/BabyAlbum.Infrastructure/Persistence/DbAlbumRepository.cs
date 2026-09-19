using BabyAlbum.Application.Albums;
using BabyAlbum.Domain.Albums;
using BabyAlbum.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace BabyAlbum.Infrastructure.Persistence;

public sealed class DbAlbumRepository : IAlbumRepository
{
    private const int MaxPhotosPerPage = 3;
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

    public async Task<IReadOnlyList<AlbumPhoto>> ListPhotosAsync(Guid albumId, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        return await _dbContext.Photos
            .AsNoTracking()
            .Include(photo => photo.AlbumPage)
            .Where(photo => photo.AlbumPage != null && photo.AlbumPage.AlbumId == albumId)
            .OrderBy(photo => photo.AlbumPage!.PageNumber)
            .ThenBy(photo => photo.SortOrder)
            .Select(photo => new AlbumPhoto(
                photo.Id,
                photo.AlbumPageId,
                photo.AlbumPage!.PageNumber,
                photo.Url,
                photo.Alt,
                photo.Caption,
                photo.StorageProvider,
                photo.StorageKey,
                photo.SortOrder))
            .ToListAsync(cancellationToken);
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

    public async Task<AlbumPage> AddPageAsync(Guid albumId, LayoutType layout, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        var albumExists = await _dbContext.Albums
            .AnyAsync(album => album.Id == albumId, cancellationToken);

        if (!albumExists)
        {
            throw new InvalidOperationException("Album was not found.");
        }

        var nextPageNumber = await _dbContext.AlbumPages
            .Where(page => page.AlbumId == albumId)
            .Select(page => (int?)page.PageNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var pageRecord = new AlbumPageRecord
        {
            Id = Guid.NewGuid(),
            AlbumId = albumId,
            PageNumber = nextPageNumber + 1,
            Layout = layout,
            Title = $"Page {nextPageNumber + 1}",
            DateLabel = "Family archive",
            Text = "Add photos and a caption for this album page."
        };

        _dbContext.AlbumPages.Add(pageRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return pageRecord.ToDomain();
    }

    public async Task DeletePageAsync(Guid albumId, Guid pageId, CancellationToken cancellationToken)
    {
        var pages = await _dbContext.AlbumPages
            .Include(page => page.Photos)
            .Where(page => page.AlbumId == albumId)
            .OrderBy(page => page.PageNumber)
            .ToListAsync(cancellationToken);

        var page = pages.FirstOrDefault(item => item.Id == pageId);
        if (page is null)
        {
            throw new InvalidOperationException("Album page was not found.");
        }

        if (pages.Count <= 1)
        {
            throw new InvalidOperationException("The album must keep at least one page.");
        }

        if (page.Photos.Count > 0)
        {
            throw new InvalidOperationException("Move or remove this page's photos before deleting it.");
        }

        var pagesToRenumber = pages
            .Where(item => item.Id != pageId && item.PageNumber > page.PageNumber)
            .OrderBy(item => item.PageNumber)
            .ToList();

        foreach (var item in pagesToRenumber)
        {
            item.PageNumber += 1000;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.AlbumPages.Remove(page);

        foreach (var item in pagesToRenumber)
        {
            item.PageNumber -= 1001;
        }

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

        if (page.Photos.Count >= MaxPhotosPerPage)
        {
            throw new InvalidOperationException("A page can contain up to 3 photos.");
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

    public async Task AssignPhotoToPageAsync(Guid albumId, Guid pageId, Guid photoId, int sortOrder, CancellationToken cancellationToken)
    {
        var targetPage = await _dbContext.AlbumPages
            .Include(page => page.Photos)
            .FirstOrDefaultAsync(page => page.Id == pageId && page.AlbumId == albumId, cancellationToken);

        if (targetPage is null)
        {
            throw new InvalidOperationException("Album page was not found.");
        }

        var photo = await _dbContext.Photos
            .Include(item => item.AlbumPage)
            .FirstOrDefaultAsync(item => item.Id == photoId, cancellationToken);

        if (photo?.AlbumPage is null || photo.AlbumPage.AlbumId != albumId)
        {
            throw new InvalidOperationException("Photo was not found in this album.");
        }

        var sourcePageId = photo.AlbumPageId;
        var isMovingToDifferentPage = sourcePageId != targetPage.Id;
        if (isMovingToDifferentPage && targetPage.Photos.Count >= MaxPhotosPerPage)
        {
            throw new InvalidOperationException("A page can contain up to 3 photos.");
        }

        var targetPhotos = targetPage.Photos
            .Where(item => item.Id != photo.Id)
            .OrderBy(item => item.SortOrder)
            .ToList();

        photo.AlbumPageId = targetPage.Id;
        photo.SortOrder = Math.Clamp(sortOrder, 1, MaxPhotosPerPage);
        targetPhotos.Insert(Math.Min(photo.SortOrder - 1, targetPhotos.Count), photo);
        NormalizePhotoOrder(targetPhotos);

        if (isMovingToDifferentPage)
        {
            var sourcePhotos = await _dbContext.Photos
                .Where(item => item.AlbumPageId == sourcePageId && item.Id != photo.Id)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(cancellationToken);
            NormalizePhotoOrder(sourcePhotos);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePhotoAsync(Guid photoId, string alt, string caption, CancellationToken cancellationToken)
    {
        var photo = await _dbContext.Photos
            .FirstOrDefaultAsync(item => item.Id == photoId, cancellationToken);

        if (photo is null)
        {
            throw new InvalidOperationException("Photo was not found.");
        }

        photo.Alt = TrimToLength(alt, 240);
        photo.Caption = TrimToLength(caption, 500);
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

    private static void NormalizePhotoOrder(IReadOnlyList<PhotoRecord> photos)
    {
        for (var index = 0; index < photos.Count; index++)
        {
            photos[index].SortOrder = index + 1;
        }
    }

    private static string TrimToLength(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
