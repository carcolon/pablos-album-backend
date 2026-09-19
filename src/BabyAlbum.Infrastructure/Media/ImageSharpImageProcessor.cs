using BabyAlbum.Application.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace BabyAlbum.Infrastructure.Media;

public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public async Task<ProcessedImage> PrepareForStorageAsync(
        IncomingImage image,
        long maxSizeBytes,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(image.FileName);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidImageException("Only JPG, PNG and WebP images are currently accepted.");
        }

        var original = new MemoryStream();
        await image.Content.CopyToAsync(original, cancellationToken);
        original.Position = 0;

        try
        {
            using var loadedImage = await Image.LoadAsync(original, cancellationToken);

            if (image.SizeBytes <= maxSizeBytes)
            {
                original.Position = 0;
                return new ProcessedImage(
                    SanitizeFileName(image.FileName),
                    NormalizeContentType(image.DeclaredContentType, extension),
                    original,
                    original.Length,
                    false);
            }

            original.Dispose();
            return await CompressToLimitAsync(loadedImage, image.FileName, maxSizeBytes, cancellationToken);
        }
        catch (UnknownImageFormatException exception)
        {
            original.Dispose();
            throw new InvalidImageException($"The uploaded file is not a valid image: {exception.Message}");
        }
    }

    private static async Task<ProcessedImage> CompressToLimitAsync(
        Image source,
        string originalFileName,
        long maxSizeBytes,
        CancellationToken cancellationToken)
    {
        var scale = 1.0;
        MemoryStream? bestAttempt = null;

        while (scale >= 0.35)
        {
            using var working = source.Clone(context =>
            {
                if (scale < 0.999)
                {
                    context.Resize(
                        Math.Max(1, (int)Math.Round(source.Width * scale)),
                        Math.Max(1, (int)Math.Round(source.Height * scale)));
                }
            });

            for (var quality = 88; quality >= 58; quality -= 6)
            {
                var output = new MemoryStream();
                await working.SaveAsJpegAsync(output, new JpegEncoder { Quality = quality }, cancellationToken);
                output.Position = 0;

                if (bestAttempt is null || output.Length < bestAttempt.Length)
                {
                    await (bestAttempt?.DisposeAsync() ?? ValueTask.CompletedTask);
                    bestAttempt = output;
                }
                else
                {
                    output.Dispose();
                }

                if (bestAttempt is not null && bestAttempt.Length <= maxSizeBytes)
                {
                    return new ProcessedImage(
                        $"{Path.GetFileNameWithoutExtension(SanitizeFileName(originalFileName))}.jpg",
                        "image/jpeg",
                        bestAttempt,
                        bestAttempt.Length,
                        true);
                }
            }

            scale -= 0.12;
        }

        if (bestAttempt is null)
        {
            throw new InvalidImageException("The image could not be compressed.");
        }

        if (bestAttempt.Length > maxSizeBytes)
        {
            await bestAttempt.DisposeAsync();
            throw new InvalidImageException("The image could not be compressed below 10 MB.");
        }

        return new ProcessedImage(
            $"{Path.GetFileNameWithoutExtension(SanitizeFileName(originalFileName))}.jpg",
            "image/jpeg",
            bestAttempt,
            bestAttempt.Length,
            true);
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalid, '-');
        }

        return string.IsNullOrWhiteSpace(safeName) ? $"upload-{Guid.NewGuid():N}.jpg" : safeName;
    }

    private static string NormalizeContentType(string declaredContentType, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => string.IsNullOrWhiteSpace(declaredContentType) ? "application/octet-stream" : declaredContentType
        };
    }
}
