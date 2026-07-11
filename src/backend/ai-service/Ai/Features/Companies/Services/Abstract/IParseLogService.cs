using Sellevate.Ai.Features.Companies.Models;

namespace Sellevate.Ai.Features.Companies.Services.Abstract;

public interface IParseLogService
{
    /// <summary>
    /// Asks the configured LLM to extract a structured call-log entry (contact, subject, outcome,
    /// and an optional occurred-at date) from free-form pasted notes/transcript. Stateless — the
    /// raw text is the only input; nothing is read from a database here. Throws
    /// <see cref="InvalidOperationException"/> if the AI is not configured or its response cannot
    /// be parsed as the expected JSON shape.
    /// </summary>
    Task<ParsedCallLogDto> ParseLogAsync(string rawText, CancellationToken cancellationToken = default);
}
