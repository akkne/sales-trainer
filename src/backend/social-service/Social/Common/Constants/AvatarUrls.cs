namespace Sellevate.Social.Common.Constants;

public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
