using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Services.Abstract;

/// <summary>
/// The dialog roleplay lifecycle. Every method that names a session takes the caller's user id and
/// resolves the session under it, so no overload of this interface can reach another learner's
/// conversation.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// <see langword="false"/> when no provider key is configured. Callers answer with an empty list or a
    /// 503 rather than letting the first provider call fail.
    /// </summary>
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

    Task<DialogMode?> GetCompanyCallModeAsync(
        CancellationToken cancellationToken = default);

    Task<DialogMode?> GetCustomScenarioModeAsync(
        CancellationToken cancellationToken = default);

    Task<DialogSession> StartSessionAsync(
        Guid userId,
        Guid bundleId,
        Guid modeId,
        CompanyCallContext? companyCallContext,
        CustomScenarioContext? customScenarioContext,
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

    Task<DialogFeedbackResult?> CompleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
