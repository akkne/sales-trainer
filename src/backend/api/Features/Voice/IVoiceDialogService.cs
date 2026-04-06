using SalesTrainer.Api.Features.Dialog;

namespace SalesTrainer.Api.Features.Voice;

public interface IVoiceDialogService
{
    Task<Stream> ProcessVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default);
}
