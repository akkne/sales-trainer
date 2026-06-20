namespace Sellevate.Identity.Features.Avatars;

public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
