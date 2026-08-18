namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// The object-store layout for avatar images, in one place so the seeder that writes a stock avatar and
/// the service that reads it back cannot disagree about where it lives.
///
/// <para>
/// These keys are also persisted — <c>DefaultAvatar.ObjectKey</c> and <c>User.AvatarKey</c> — so changing
/// a shape here orphans every object already in the store rather than moving it.
/// </para>
/// </summary>
internal static class AvatarObjectKeys
{
    public const string DefaultAvatarContentType = "image/png";

    public static string DefaultAvatarFileName(int catalogIndex) => $"avatar-{catalogIndex:00}.png";

    public static string ForDefaultAvatar(int catalogIndex) => $"defaults/{DefaultAvatarFileName(catalogIndex)}";

    public static string ForUploadedAvatar(Guid userId, string fileExtension)
        => $"users/{userId}/avatar{fileExtension}";
}
