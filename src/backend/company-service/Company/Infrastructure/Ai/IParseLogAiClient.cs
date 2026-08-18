namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Extracts the fields of a draft call-log entry from raw pasted text via ai-service. The result is
/// a suggestion for a person to review, never something to save unseen; individual fields may come
/// back empty or with no date at all.
/// </summary>
public interface IParseLogAiClient
{
    Task<ParseLogAiResult> ParseLogAsync(
        ParseLogAiRequest request,
        CancellationToken cancellationToken = default);
}
