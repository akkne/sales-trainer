namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// The single place that knows the shape of a user's avatar URL. Every DTO that carries one builds it
/// here so the route and the payloads cannot drift apart.
/// </summary>
public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
