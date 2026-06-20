using Sellevate.Ai.Features.Voice.Models;

namespace Sellevate.Ai.Features.Voice.Services.Abstract;

public interface IVoiceUsageService
{
    Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsureWithinLimitsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken cancellationToken = default);
    Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default);
}
