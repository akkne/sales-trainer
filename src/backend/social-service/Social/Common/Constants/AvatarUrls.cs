namespace Sellevate.Social.Common.Constants;

/// <summary>
/// Builds the avatar URL for a user id. Not a stored value: the shape is a route the gateway resolves
/// to identity-service, so an avatar changing never invalidates anything social-service holds — see
/// docs/USER_AVATARS.md. Every projection in this service goes through here rather than composing the
/// path itself, so the route is changed in one place.
/// </summary>
public static class AvatarUrls
{
    public static string For(Guid userId) => $"/avatars/{userId}";
}
