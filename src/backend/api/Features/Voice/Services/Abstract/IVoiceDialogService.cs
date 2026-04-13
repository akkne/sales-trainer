namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IVoiceDialogService
{
    Task<Stream> ProcessVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default);
}
