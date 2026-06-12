namespace SalesTrainer.Api.Features.Discuss.Services;

internal static class DiscussPhotoUrlBuilder
{
    public static string Build(Guid photoId) => $"/discuss/photos/{photoId}/content";
}
