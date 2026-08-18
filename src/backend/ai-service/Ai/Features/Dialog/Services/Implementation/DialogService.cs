using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Helpers;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Overrides;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Organizations;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Learning;

namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

/// <summary>
/// The dialog roleplay lifecycle: choose a mode, start a session, exchange turns, grade the result.
///
/// <para>
/// <b>Prompt assembly has a fixed order and the order is the contract</b> (Phase 40.19). The mode's own
/// <c>{{organization.*}}</c> placeholders are resolved first, so a base persona written once serves
/// every customer instead of being forked per customer. Then the human-authored data blocks — company,
/// scenario, assignment — are appended, each fenced as data rather than instructions. The banned-claims
/// compliance rule goes last, after every block that carries text a human wrote, because a rule that
/// something later can qualify is not a rule. The grader is given the same blocks as the character: a
/// persona that stays silent while the grader keeps rewarding the forbidden claim teaches it anyway.
/// </para>
///
/// <para>
/// <b>Context is resolved once and frozen on the session.</b> The assignment persona is asked of
/// learning-service and never accepted from the request — the learner is the person being graded
/// against that persona — and it is resolved at start so editing or closing the assignment mid-call
/// cannot change the character they are already talking to (Phase 40.23).
/// </para>
///
/// <para>
/// <b>A custom scenario is re-validated here even though the client already validated it.</b> That
/// earlier call exists to give fast feedback in the compose dialog and proves nothing about what
/// arrives; re-checking is what enforces the rule, and it is near-free because the text hashes to the
/// cache key the client's call just populated.
/// </para>
///
/// <para>
/// <b>A conversation with no learner turn is abandoned, not graded.</b> It yields no feedback and no
/// award, so an accidental open-and-close cannot enter the record as a failed call.
/// </para>
/// </summary>
internal sealed class DialogService : IDialogService
{
    private readonly AiDbContext _databaseContext;
    private readonly IDialogSessionRepository _sessionRepository;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly IDialogScoringWeightsProvider _scoringWeightsProvider;
    private readonly IDialogEventPublisher _dialogEventPublisher;
    private readonly IScenarioValidationService _scenarioValidationService;
    private readonly IOrganizationProfileProvider _organizationProfileProvider;
    private readonly IAssignmentPracticeContextClient _assignmentPracticeContextClient;
    private readonly ILogger<DialogService> _logger;

    public DialogService(
        AiDbContext databaseContext,
        IDialogSessionRepository sessionRepository,
        IOpenAiChatService openAiChatService,
        IDialogScoringWeightsProvider scoringWeightsProvider,
        IDialogEventPublisher dialogEventPublisher,
        IScenarioValidationService scenarioValidationService,
        IOrganizationProfileProvider organizationProfileProvider,
        IAssignmentPracticeContextClient assignmentPracticeContextClient,
        ILogger<DialogService> logger)
    {
        _databaseContext = databaseContext;
        _sessionRepository = sessionRepository;
        _openAiChatService = openAiChatService;
        _scoringWeightsProvider = scoringWeightsProvider;
        _dialogEventPublisher = dialogEventPublisher;
        _scenarioValidationService = scenarioValidationService;
        _organizationProfileProvider = organizationProfileProvider;
        _assignmentPracticeContextClient = assignmentPracticeContextClient;
        _logger = logger;
    }

    public bool IsOpenAiConfigured => _openAiChatService.IsConfigured;

    public async Task<List<DialogBundle>> GetActiveBundlesAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogBundles
            .Where(bundle => bundle.IsActive && !bundle.IsHidden)
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<DialogBundle?> GetBundleByIdAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogBundles
            .FirstOrDefaultAsync(bundle => bundle.Id == bundleId, cancellationToken);
    }

    /// <summary>
    /// Phase 40.18. Resolved and inside a transaction, both for the same reason: this is the only
    /// learner-facing list of prompts. Without resolution an organization that overrode one mode would
    /// see it twice in the same bundle; without the transaction <c>SET LOCAL</c> never runs and its own
    /// override would not be visible at all.
    /// </summary>
    public async Task<List<DialogMode>> GetActiveModesForBundleAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await AiTenantTransactionScope.BeginReadAsync(_databaseContext, cancellationToken);

        return await _databaseContext.DialogModes
            .ResolveOverrides(_databaseContext)
            .Where(mode => mode.BundleId == bundleId && mode.IsActive)
            .OrderBy(mode => mode.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<DialogMode?> GetModeByIdAsync(
        Guid modeId,
        CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogModes
            .Include(mode => mode.Bundle)
            .FirstOrDefaultAsync(mode => mode.Id == modeId, cancellationToken);
    }

    public async Task<DialogMode?> GetCompanyCallModeAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogModes
            .Include(mode => mode.Bundle)
            .FirstOrDefaultAsync(
                mode => mode.Id == CompanyCallModeSeeder.CompanyCallModeId
                    && mode.Key == DialogModeKeys.CompanyCall,
                cancellationToken);
    }

    public async Task<DialogMode?> GetCustomScenarioModeAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogModes
            .Include(mode => mode.Bundle)
            .FirstOrDefaultAsync(
                mode => mode.Id == CustomScenarioModeSeeder.CustomScenarioModeId
                    && mode.Key == DialogModeKeys.CustomScenario,
                cancellationToken);
    }

    public async Task<DialogSession> StartSessionAsync(
        Guid userId,
        Guid bundleId,
        Guid modeId,
        CompanyCallContext? companyCallContext,
        CustomScenarioContext? customScenarioContext,
        CancellationToken cancellationToken = default)
    {
        var mode = await GetModeByIdAsync(modeId, cancellationToken);
        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {modeId} not found");
        }

        if (companyCallContext != null && mode.Key != DialogModeKeys.CompanyCall)
        {
            throw new InvalidOperationException(
                "companyContext may only be used with the company-call mode. " +
                "Obtain the correct bundleId and modeId from GET /dialog/company-call-mode.");
        }

        if (customScenarioContext != null && mode.Key != DialogModeKeys.CustomScenario)
        {
            throw new InvalidOperationException(
                "customScenario may only be used with the custom-scenario mode. " +
                "Obtain the correct bundleId and modeId from GET /dialog/custom-scenario-mode.");
        }

        if (mode.Key == DialogModeKeys.CustomScenario && customScenarioContext == null)
        {
            throw new InvalidOperationException(DialogMessages.ScenarioDescriptionRequired);
        }

        if (customScenarioContext != null)
        {
            var verdict = await _scenarioValidationService.ValidateAsync(
                customScenarioContext.Scenario, cancellationToken);

            if (!verdict.IsValid)
            {
                throw new ScenarioRejectedException(
                    verdict.RejectionReason ?? DialogMessages.ScenarioNotAboutSales);
            }

            customScenarioContext.Scenario = customScenarioContext.Scenario.Trim();
        }

        var assignmentPracticeContext =
            await _assignmentPracticeContextClient.GetPracticeContextAsync(userId, mode.Key, cancellationToken);

        var session = new DialogSession
        {
            UserId = userId,
            BundleId = bundleId,
            ModeId = modeId,
            Status = DialogSessionStatus.Active,
            Messages = [],
            CompanyCallContext = companyCallContext,
            CustomScenarioContext = customScenarioContext,
            AssignmentPracticeContext = assignmentPracticeContext
        };

        await _sessionRepository.InsertAsync(session, cancellationToken);
        _logger.LogInformation("Started dialog session {SessionId} for user {UserId}", session.Id, userId);

        return session;
    }

    public async Task<DialogSession?> GetSessionForUserAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _sessionRepository.FindForUserAsync(sessionId, userId, cancellationToken);

    public async Task<List<DialogSession>> GetUserSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _sessionRepository.ListForUserAsync(userId, cancellationToken);

    public async Task<DialogMessage> SendMessageAsync(
        string sessionId,
        Guid userId,
        string userMessageContent,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionForUserAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        }

        if (session.Status != DialogSessionStatus.Active)
        {
            throw new InvalidOperationException($"Session {sessionId} is not active");
        }

        var mode = await GetModeByIdAsync(session.ModeId, cancellationToken);
        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        }

        var userMessage = new DialogMessage
        {
            Role = DialogMessageRoles.User,
            Content = userMessageContent,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        };

        session.Messages.Add(userMessage);

        var organizationProfile = await _organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var modeChatPrompt = RenderModePrompt(mode.ChatSystemPrompt, organizationProfile);

        var chatSystemPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(modeChatPrompt, session.CompanyCallContext);
        chatSystemPrompt = CustomScenarioPromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.CustomScenarioContext);
        chatSystemPrompt = AssignmentPracticePromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.AssignmentPracticeContext);
        chatSystemPrompt += OrganizationProfilePromptBuilder.BuildContextBlock(organizationProfile);
        chatSystemPrompt += OrganizationProfilePromptBuilder.BuildPersonaBannedClaimsBlock(organizationProfile);
        var chatResult = await _openAiChatService.SendChatMessageAsync(chatSystemPrompt, session.Messages, cancellationToken);

        var aiMessage = new DialogMessage
        {
            Role = DialogMessageRoles.Assistant,
            Content = chatResult.Content,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = chatResult.IsStopSignal
        };

        session.Messages.Add(aiMessage);

        await _sessionRepository.AppendMessagesAsync(
            sessionId, userId, [userMessage, aiMessage], cancellationToken);

        _logger.LogInformation("Added message to session {SessionId}, total messages: {Count}", sessionId, session.Messages.Count);

        return aiMessage;
    }

    public async Task<DialogFeedbackResult?> CompleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionForUserAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        }

        if (session.Status != DialogSessionStatus.Active)
        {
            throw new InvalidOperationException($"Session {sessionId} is not active");
        }

        if (!session.Messages.Any(message => message.Role == DialogMessageRoles.User && !string.IsNullOrWhiteSpace(message.Content)))
        {
            await _sessionRepository.AbandonAsync(sessionId, userId, cancellationToken);

            _logger.LogInformation("Abandoned empty session {SessionId} for user {UserId} — no user messages to evaluate", sessionId, userId);
            return null;
        }

        var mode = await GetModeByIdAsync(session.ModeId, cancellationToken);
        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        }

        var scoringWeights = _scoringWeightsProvider.Current;
        var xpWeights = new DialogXpWeights(
            scoringWeights.Confidence,
            scoringWeights.Structure,
            scoringWeights.Objection,
            scoringWeights.Goal);

        var organizationProfile = await _organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var modeFeedbackPrompt = RenderModePrompt(mode.FeedbackSystemPrompt, organizationProfile);

        var feedbackSystemPrompt = CompanyContextPromptBuilder.BuildFeedbackSystemPrompt(modeFeedbackPrompt, session.CompanyCallContext);
        feedbackSystemPrompt = CustomScenarioPromptBuilder.BuildFeedbackSystemPrompt(feedbackSystemPrompt, session.CustomScenarioContext);
        feedbackSystemPrompt = AssignmentPracticePromptBuilder.BuildFeedbackSystemPrompt(feedbackSystemPrompt, session.AssignmentPracticeContext);
        feedbackSystemPrompt += OrganizationProfilePromptBuilder.BuildContextBlock(organizationProfile);
        feedbackSystemPrompt += OrganizationProfilePromptBuilder.BuildEvaluationBannedClaimsBlock(organizationProfile);
        var feedbackResult = await _openAiChatService.GenerateFeedbackAsync(feedbackSystemPrompt, session.Messages, xpWeights, cancellationToken);

        var earnedXp = (int)Math.Round(feedbackResult.XpReward * scoringWeights.Multiplier);

        var feedback = new DialogFeedback
        {
            Summary = feedbackResult.Summary,
            Content = feedbackResult.Content,
            Score = feedbackResult.Score,
            GeneratedAt = DateTime.UtcNow
        };

        await _sessionRepository.CompleteAsync(sessionId, userId, feedback, earnedXp, cancellationToken);

        await _dialogEventPublisher.PublishEvaluatedAsync(
            new DialogEvaluatedEvent(
                userId,
                sessionId,
                session.BundleId,
                session.ModeId,
                feedbackResult.XpReward,
                earnedXp,
                mode.Key,
                NormalizeScoreForLearningService(feedbackResult.Score)),
            cancellationToken);

        _logger.LogInformation("Completed session {SessionId} for user {UserId}, XP earned: {ExperiencePoints}", sessionId, userId, earnedXp);

        return new DialogFeedbackResult
        {
            Feedback = feedback,
            XpEarned = earnedXp
        };
    }

    public async Task<bool> DeleteSessionAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _sessionRepository.DeleteForUserAsync(sessionId, userId, cancellationToken);
        if (deleted)
        {
            _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);
        }

        return deleted;
    }

    /// <summary>
    /// Phase 40.22. Converts the 0–10 grade the learner sees onto the 0–100 scale every score in
    /// learning-service is already on, so an assignment's threshold («оценка >= 70») is comparable
    /// without the consumer knowing this service's internal scale. Clamped first: the grade travels in
    /// an event, and a model that ignored the stated range must not move a threshold.
    /// </summary>
    private static int NormalizeScoreForLearningService(int score)
        => Math.Clamp(score, DialogScoreScale.Minimum, DialogScoreScale.Maximum)
           * DialogScoreScale.LearningServiceScaleFactor;

    /// <summary>
    /// Phase 40.19. Resolves <c>{{organization.*}}</c> in a stored mode prompt.
    ///
    /// <para>
    /// Rendered here on the way to the model, never written back: <c>DialogMode.ChatSystemPrompt</c>
    /// stays the template, which is what keeps its 40.18 <c>BaseContentHash</c> the same for every
    /// organization and the stale queue honest about whether upstream actually moved.
    /// </para>
    ///
    /// <para>
    /// Placeholders outside the <c>organization.</c> namespace pass through untouched — the seeded
    /// hidden modes complete their prompts from placeholders the code supplies at run time
    /// (docs/TENANCY/CONTENT_MODEL.md §4), and eating those would break company-call practice.
    /// </para>
    /// </summary>
    private string RenderModePrompt(string? prompt, OrganizationProfileSnapshot profile)
    {
        if (!OrganizationPlaceholderRenderer.HasOrganizationPlaceholders(prompt))
        {
            return prompt ?? string.Empty;
        }

        var unresolved = new List<string>();
        var rendered = OrganizationPlaceholderRenderer.Render(prompt, profile, unresolved);

        if (unresolved.Count > 0)
        {
            _logger.LogWarning(
                "Unresolved organization placeholders in a dialog mode prompt: {Placeholders}",
                string.Join(", ", unresolved.Distinct()));
        }

        return rendered;
    }
}
