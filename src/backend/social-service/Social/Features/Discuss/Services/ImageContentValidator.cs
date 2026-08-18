using Sellevate.Social.Features.Discuss.Constants;

namespace Sellevate.Social.Features.Discuss.Services;

/// <summary>
/// Decides whether an uploaded file may be stored as a photo, and what content type it will be served
/// with later.
///
/// <para>
/// The extension is checked against an allow-list <em>and</em> the leading bytes are checked against
/// the magic numbers of the three accepted formats, because a name is chosen by the uploader while
/// the header is not: a script renamed to <c>.png</c> passes the first test and fails the second. The
/// content type returned is derived from the extension of a file that already proved its header, so a
/// caller may store it and serve it back without consulting the uploader's claim again.
/// </para>
///
/// <para>
/// The stream is rewound when it supports seeking, so the caller can upload the same stream it passed
/// in. A stream that cannot seek is left where the header read left it — the caller must not reuse it.
/// </para>
/// </summary>
internal static class ImageContentValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        DiscussPhotoConstants.PngExtension,
        DiscussPhotoConstants.JpgExtension,
        DiscussPhotoConstants.JpegExtension,
        DiscussPhotoConstants.WebpExtension
    };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffHeader = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpMarker = [0x57, 0x45, 0x42, 0x50];

    /// <summary>
    /// Bytes read up front: enough for the longest check, which is WEBP's marker at
    /// <see cref="WebpMarkerOffset"/>.
    /// </summary>
    private const int HeaderLength = 12;

    /// <summary>Shortest signature accepted, so a file below this cannot match anything.</summary>
    private const int MinimumHeaderLength = 3;

    /// <summary>A WEBP file is a RIFF container whose marker sits after the 4-byte chunk size.</summary>
    private const int WebpMarkerOffset = 8;

    public static async Task<ImageContentValidationResult> ValidateAsync(
        Stream content,
        string fileName,
        long length,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (length <= 0 || length > DiscussPhotoConstants.MaximumFileSizeBytes)
            return ImageContentValidationResult.Invalid;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return ImageContentValidationResult.Invalid;

        var header = new byte[HeaderLength];
        var headerBytesRead = await content.ReadAtLeastAsync(
            header.AsMemory(0, HeaderLength),
            minimumBytes: HeaderLength,
            throwOnEndOfStream: false,
            cancellationToken);
        if (content.CanSeek)
            content.Seek(0, SeekOrigin.Begin);

        if (headerBytesRead < MinimumHeaderLength || !HasValidImageMagicBytes(header))
            return ImageContentValidationResult.Invalid;

        var contentType = ResolveContentType(extension);
        return new ImageContentValidationResult(IsValid: true, contentType, extension);
    }

    private static bool HasValidImageMagicBytes(byte[] header) =>
        MatchesAt(header, 0, PngSignature)
        || MatchesAt(header, 0, JpegSignature)
        || (MatchesAt(header, 0, RiffHeader) && MatchesAt(header, WebpMarkerOffset, WebpMarker));

    /// <summary>
    /// Whether the header carries <paramref name="signature"/> at <paramref name="offset"/>. A header
    /// too short to hold it does not match, rather than throwing — a truncated file is an invalid one.
    /// </summary>
    private static bool MatchesAt(byte[] header, int offset, byte[] signature) =>
        header.Length >= offset + signature.Length
        && header.AsSpan(offset, signature.Length).SequenceEqual(signature);

    private static string ResolveContentType(string extension) => extension switch
    {
        DiscussPhotoConstants.PngExtension => DiscussPhotoConstants.PngContentType,
        DiscussPhotoConstants.JpgExtension => DiscussPhotoConstants.JpegContentType,
        DiscussPhotoConstants.JpegExtension => DiscussPhotoConstants.JpegContentType,
        DiscussPhotoConstants.WebpExtension => DiscussPhotoConstants.WebpContentType,
        _ => DiscussPhotoConstants.JpegContentType
    };
}
