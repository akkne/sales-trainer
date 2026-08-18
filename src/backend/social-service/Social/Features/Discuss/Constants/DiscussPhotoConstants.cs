namespace Sellevate.Social.Features.Discuss.Constants;

/// <summary>
/// Everything the photo endpoints agree on: what may be uploaded, how big, where the object lands,
/// and how the anonymous content endpoint answers.
///
/// <para>
/// These are <c>const</c> rather than configuration because <c>[RequestSizeLimit]</c> and
/// <c>[RequestFormLimits]</c> are attributes and take compile-time values only. Raising a limit is
/// therefore a rebuild, and <see cref="MaximumUploadRequestSizeBytes"/> must stay derived from the
/// per-file limit — a request cap below what the per-file cap allows would reject a legal upload at
/// the pipeline before the validator ever ran.
/// </para>
/// </summary>
internal static class DiscussPhotoConstants
{
    public const int MaximumPhotosPerOwner = 10;
    public const long MaximumFileSizeBytes = 5 * 1024 * 1024;

    /// <summary>The per-file cap for a full batch, plus a megabyte of multipart framing.</summary>
    public const long MaximumUploadRequestSizeBytes = MaximumFileSizeBytes * MaximumPhotosPerOwner + 1024 * 1024;

    public const int MaximumObjectKeyLength = 512;
    public const int MaximumContentTypeLength = 100;

    /// <summary>
    /// Short and public: the bytes behind a photo id never change, but the row granting access to
    /// them can be deleted, so a long-lived cache would keep serving a deleted photo.
    /// </summary>
    public const string ContentCacheControl = "public, max-age=60";

    /// <summary>
    /// Phase 40.13. Leading segment of every object key written from now on, followed by the
    /// organization id — see <c>DiscussService.ResolveObjectKeyPrefix</c> for why the bucket carries
    /// the tenant even though the tenant boundary is the database row.
    /// </summary>
    public const string OrganizationObjectKeyPrefix = "org";

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
