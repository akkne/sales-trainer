namespace Sellevate.Social.Features.Discuss.Services;

/// <summary>
/// Builds the URL a client fetches a discussion photo from. The photo id is the whole address: the
/// object key never leaves the server, so the storage layout can change without breaking a rendered
/// page, and the route must stay in step with <c>DiscussController.GetPhotoContent</c>.
/// </summary>
internal static class DiscussPhotoUrlBuilder
{
    public static string Build(Guid photoId) => $"/discuss/photos/{photoId}/content";
}
