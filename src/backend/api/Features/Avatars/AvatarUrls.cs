namespace SalesTrainer.Api.Features.Avatars;

public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
