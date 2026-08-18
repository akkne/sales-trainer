namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Media types that are part of a wire contract rather than a local choice: the frame stream the
/// voice client decodes, the newline-delimited stream a sibling service parses, and the
/// server-sent-event type an OpenAI-compatible provider answers a streaming completion with.
/// Changing one of these breaks a reader that cannot be found by compiling this project.
/// </summary>
public static class AiMediaTypes
{
    public const string Json = "application/json";

    /// <summary>Length-prefixed text/audio frames, read by the browser voice client.</summary>
    public const string OctetStream = "application/octet-stream";

    /// <summary>One JSON object per line, read by learning-service's exercise dialogue.</summary>
    public const string NewlineDelimitedJson = "application/x-ndjson";

    /// <summary>What a provider answers with when it honoured <c>stream: true</c>.</summary>
    public const string ServerSentEvents = "text/event-stream";

    public const string WavAudio = "audio/wav";
}
