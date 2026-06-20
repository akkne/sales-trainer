namespace Sellevate.Gamification.Features.League.Models;

public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
