using SalesTrainer.Api.Features.Dialog.Models;

namespace SalesTrainer.Api.Features.Dialog.Services.Abstract;

public interface IDialogService
{
    bool IsOpenAiConfigured { get; }

    Task<List<DialogBundle>> GetActiveBundlesAsync(
        CancellationToken cancellationToken = default);

    Task<DialogBundle?> GetBundleByIdAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default);

    Task<List<DialogMode>> GetActiveModesForBundleAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default);

    Task<DialogMode?> GetModeByIdAsync(
        Guid modeId,
        CancellationToken cancellationToken = default);

    Task<DialogSession> StartSessionAsync(
        Guid userId,
        Guid bundleId,
        Guid modeId,
        CancellationToken cancellationToken = default);

    Task<DialogSession?> GetSessionByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<DialogSession?> GetSessionForUserAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<DialogSession>> GetUserSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DialogMessage> SendMessageAsync(
        string sessionId,
        Guid userId,
        string userMessageContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the session and generates AI feedback. Returns null when the
    /// session contains no user messages — such sessions are marked abandoned
    /// without invoking the feedback model (nothing to evaluate).
    /// </summary>
    Task<DialogFeedbackResult?> CompleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
