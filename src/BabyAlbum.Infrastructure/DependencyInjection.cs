using BabyAlbum.Application.Albums;
using BabyAlbum.Application.Media;
using BabyAlbum.Infrastructure.Media;
using BabyAlbum.Infrastructure.Persistence;
using BabyAlbum.Infrastructure.Storage.GoogleDrive;
using BabyAlbum.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BabyAlbum.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(ConnectionStringFactory.GetDefaultConnection(configuration));
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAlbumRepository, DbAlbumRepository>();
        services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
        services.AddScoped<IMediaStorage, GoogleDriveMediaStorage>();
        return services;
    }
}
