namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// The AI provider rejected the request or answered with something we cannot use
/// (invalid request, provider-side failure, unparseable body). This is an expected
/// operational condition, not a defect: callers map it to a 502/503 instead of
/// letting it escape as an unhandled 500.
/// </summary>
public sealed class OpenAiRequestException(string message, int? statusCode = null) : OpenAiException(message)
{
    /// <summary>Upstream HTTP status, when the failure came from a response rather than a parse.</summary>
    public int? StatusCode { get; } = statusCode;
}
