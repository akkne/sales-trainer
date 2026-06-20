using Sellevate.Social.Features.Discuss.Constants;

namespace Sellevate.Social.Features.Discuss.Services;

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

    private const int HeaderLength = 12;
    private const int MinimumHeaderLength = 3;

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
        var headerBytesRead = await content.ReadAsync(header.AsMemory(0, HeaderLength), cancellationToken);
        if (content.CanSeek)
            content.Seek(0, SeekOrigin.Begin);

        if (headerBytesRead < MinimumHeaderLength || !HasValidImageMagicBytes(header))
            return ImageContentValidationResult.Invalid;

        var contentType = ResolveContentType(extension);
        return new ImageContentValidationResult(IsValid: true, contentType, extension);
    }

    private static bool HasValidImageMagicBytes(byte[] header)
    {
        if (header.Length >= 4
                && header[0] == PngSignature[0] && header[1] == PngSignature[1]
                && header[2] == PngSignature[2] && header[3] == PngSignature[3])
            return true;

        if (header.Length >= 3
                && header[0] == JpegSignature[0] && header[1] == JpegSignature[1]
                && header[2] == JpegSignature[2])
            return true;

        if (header.Length >= 12
                && header[0] == RiffHeader[0] && header[1] == RiffHeader[1]
                && header[2] == RiffHeader[2] && header[3] == RiffHeader[3]
                && header[8] == WebpMarker[0] && header[9] == WebpMarker[1]
                && header[10] == WebpMarker[2] && header[11] == WebpMarker[3])
            return true;

        return false;
    }

    private static string ResolveContentType(string extension) => extension switch
    {
        DiscussPhotoConstants.PngExtension => DiscussPhotoConstants.PngContentType,
        DiscussPhotoConstants.JpgExtension => DiscussPhotoConstants.JpegContentType,
        DiscussPhotoConstants.JpegExtension => DiscussPhotoConstants.JpegContentType,
        DiscussPhotoConstants.WebpExtension => DiscussPhotoConstants.WebpContentType,
        _ => DiscussPhotoConstants.JpegContentType
    };
}
