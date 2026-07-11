namespace Sellevate.Company.Infrastructure.Ai;

public interface IParseLogAiClient
{
    Task<ParseLogAiResult> ParseLogAsync(
        ParseLogAiRequest request,
        CancellationToken cancellationToken = default);
}
