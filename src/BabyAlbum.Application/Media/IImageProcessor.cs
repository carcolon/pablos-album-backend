namespace BabyAlbum.Application.Media;

public interface IImageProcessor
{
    Task<ProcessedImage> PrepareForStorageAsync(
        IncomingImage image,
        long maxSizeBytes,
        CancellationToken cancellationToken);
}

public sealed record IncomingImage(
    string FileName,
    string DeclaredContentType,
    Stream Content,
    long SizeBytes);

public sealed record ProcessedImage(
    string FileName,
    string ContentType,
    Stream Content,
    long SizeBytes,
    bool WasCompressed) : IAsyncDisposable, IDisposable
{
    public void Dispose()
    {
        Content.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return Content.DisposeAsync();
    }
}
