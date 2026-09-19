using BabyAlbum.Infrastructure.Identity;
using BabyAlbum.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BabyAlbum.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AlbumRecord> Albums => Set<AlbumRecord>();

    public DbSet<AlbumPageRecord> AlbumPages => Set<AlbumPageRecord>();

    public DbSet<PhotoRecord> Photos => Set<PhotoRecord>();

    public DbSet<MemoryRecord> Memories => Set<MemoryRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName).HasMaxLength(120);
            entity.Property(user => user.CreatedAt);
        });

        builder.Entity<AlbumRecord>(entity =>
        {
            entity.ToTable("Albums");
            entity.Property(album => album.Title).HasMaxLength(180);
            entity.Property(album => album.Subtitle).HasMaxLength(240);
            entity.Property(album => album.Description).HasMaxLength(800);
            entity.Property(album => album.Theme).HasMaxLength(80);
            entity.Property(album => album.CoverPhotoUrl).HasMaxLength(1200);
            entity.HasMany(album => album.Pages)
                .WithOne(page => page.Album)
                .HasForeignKey(page => page.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(album => album.Memories)
                .WithOne(memory => memory.Album)
                .HasForeignKey(memory => memory.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AlbumPageRecord>(entity =>
        {
            entity.ToTable("AlbumPages");
            entity.Property(page => page.Layout).HasConversion<string>().HasMaxLength(40);
            entity.Property(page => page.Title).HasMaxLength(180);
            entity.Property(page => page.DateLabel).HasMaxLength(120);
            entity.Property(page => page.Text).HasMaxLength(2000);
            entity.HasIndex(page => new { page.AlbumId, page.PageNumber }).IsUnique();
            entity.HasMany(page => page.Photos)
                .WithOne(photo => photo.AlbumPage)
                .HasForeignKey(photo => photo.AlbumPageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PhotoRecord>(entity =>
        {
            entity.ToTable("Photos");
            entity.Property(photo => photo.Url).HasMaxLength(1200);
            entity.Property(photo => photo.Alt).HasMaxLength(240);
            entity.Property(photo => photo.Caption).HasMaxLength(500);
            entity.Property(photo => photo.ContentType).HasMaxLength(120);
            entity.Property(photo => photo.StorageProvider).HasMaxLength(80);
            entity.Property(photo => photo.StorageKey).HasMaxLength(300);
            entity.HasIndex(photo => new { photo.AlbumPageId, photo.SortOrder });
        });

        builder.Entity<MemoryRecord>(entity =>
        {
            entity.ToTable("Memories");
            entity.Property(memory => memory.Title).HasMaxLength(180);
            entity.Property(memory => memory.Description).HasMaxLength(1000);
            entity.Property(memory => memory.Type).HasMaxLength(80);
        });
    }
}
