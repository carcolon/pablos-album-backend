namespace BabyAlbum.Infrastructure.Identity;

public static class ApplicationRoles
{
    public const string Owner = "Owner";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Owner, Editor, Viewer];
}
