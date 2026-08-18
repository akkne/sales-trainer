using Sellevate.Ai.Common.Constants;

namespace Sellevate.Ai.Features.Transcription.Constants;

/// <summary>
/// The audio container formats transcription accepts, and the MIME type each one is uploaded under.
///
/// <para>
/// One table rather than two. The accepted-extension check and the MIME type sent to the provider were
/// separate lists that had to agree: an extension present in one and missing from the other either
/// rejected a format the provider handles, or uploaded it as <c>application/octet-stream</c> and let
/// the provider guess. The set is fixed by what Whisper decodes, so it is a vocabulary rather than a
/// tuning knob.
/// </para>
/// </summary>
public static class TranscriptionAudioFormats
{
    /// <summary>Sent when an extension is not in <see cref="MimeTypesByExtension"/>.</summary>
    public const string FallbackMimeType = AiMediaTypes.OctetStream;

    /// <summary>Lower-case file extension, including the dot, to the MIME type it is uploaded as.</summary>
    public static readonly IReadOnlyDictionary<string, string> MimeTypesByExtension =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".mp3"] = "audio/mpeg",
            [".mp4"] = "audio/mp4",
            [".m4a"] = "audio/mp4",
            [".mpeg"] = "audio/mpeg",
            [".mpga"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".webm"] = "audio/webm",
            [".ogg"] = "audio/ogg",
        };

    /// <summary>Every accepted extension, in the order they are listed to the caller on a rejection.</summary>
    public static readonly IReadOnlyList<string> AcceptedExtensions = MimeTypesByExtension.Keys.ToList();
}
