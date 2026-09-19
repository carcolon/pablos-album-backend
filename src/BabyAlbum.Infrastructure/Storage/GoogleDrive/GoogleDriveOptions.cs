namespace BabyAlbum.Infrastructure.Storage.GoogleDrive;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public string ApplicationName { get; set; } = "Pablo's Album";

    public string? FolderId { get; set; }

    public string? ServiceAccountJson { get; set; }

    public string? ServiceAccountJsonPath { get; set; }

    public string? OAuthClientId { get; set; }

    public string? OAuthClientSecret { get; set; }

    public string? OAuthRefreshToken { get; set; }

    public string OAuthUser { get; set; } = "me";
}
