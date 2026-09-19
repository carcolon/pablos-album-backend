using BabyAlbum.Application.Albums;
using BabyAlbum.Application.Media;
using BabyAlbum.Infrastructure.Media;
using BabyAlbum.Infrastructure.Persistence;
using BabyAlbum.Infrastructure.Storage.GoogleDrive;
using Microsoft.Extensions.DependencyInjection;

namespace BabyAlbum.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAlbumRepository, InMemoryAlbumRepository>();
        services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
        services.AddScoped<IMediaStorage, GoogleDriveMediaStorage>();
        return services;
    }
}
