namespace SalesTrainer.Api.Features.Discuss.Constants;

internal static class DiscussPhotoConstants
{
    public const int MaximumPhotosPerOwner = 10;
    public const long MaximumFileSizeBytes = 5 * 1024 * 1024;

    public const long MaximumUploadRequestSizeBytes = MaximumFileSizeBytes * MaximumPhotosPerOwner + 1024 * 1024;

    public const string ThreadObjectKeyPrefix = "discuss/threads";
    public const string ReplyObjectKeyPrefix = "discuss/replies";

    public const string PngExtension = ".png";
    public const string JpgExtension = ".jpg";
    public const string JpegExtension = ".jpeg";
    public const string WebpExtension = ".webp";

    public const string PngContentType = "image/png";
    public const string JpegContentType = "image/jpeg";
    public const string WebpContentType = "image/webp";
}
