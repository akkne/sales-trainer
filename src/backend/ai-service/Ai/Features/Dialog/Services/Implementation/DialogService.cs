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

    public async Task<List<DialogMode>> GetActiveModesForBundleAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        // Phase 40.18. Resolved and inside a transaction, both for the same reason: this is the
        // only learner-facing list of prompts. Without resolution an organization that overrode one
        // mode would see it twice in the same bundle; without the transaction SET LOCAL never runs
        // and its own override would not be visible at all.
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
            // Reachable by hand-editing the URL of a custom-scenario conversation, so the
            // message is user-facing Russian rather than an API-contract note.
            throw new InvalidOperationException("Нужно описать сценарий, чтобы начать разговор.");
        }

        if (customScenarioContext != null)
        {
            // The client already validated through POST /dialog/scenario/validate, but that call is
            // only there to give fast feedback in the dialog — it proves nothing about what arrives
            // here. Re-checking is what actually enforces the rule, and it is near-free: the text
            // hashes to the same cache key the client's call just populated.
            var verdict = await _scenarioValidationService.ValidateAsync(
                customScenarioContext.Scenario, cancellationToken);

            if (!verdict.IsValid)
            {
                throw new ScenarioRejectedException(
                    verdict.RejectionReason ?? "Недопустимый сценарий: он не связан с продажами.");
            }

            customScenarioContext.Scenario = customScenarioContext.Scenario.Trim();
        }

        // Phase 40.23. Asked for, never accepted from the request. If this conversation is a piece
        // of work somebody was assigned, learning-service says so and supplies the persona; the
        // learner's client is not consulted, because the learner is the person being graded against
        // that persona. Resolved once here and frozen on the session, so editing or closing the
        // assignment mid-conversation cannot change the character they are already talking to.
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
            Role = "user",
            Content = userMessageContent,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        };

        session.Messages.Add(userMessage);

        // Phase 40.19. Three steps, and the order is the point.
        //   1. {{organization.*}} in the mode's own prompt is resolved, so a base persona written
        //      once («ты закупщик, которому продают {{organization.product}}») serves every customer
        //      instead of being forked per customer.
        //   2. The company/scenario blocks are appended as before.
        //   3. The banned-claims rule goes LAST, after every block that carries text a human wrote,
        //      because a compliance rule that something later can qualify is not a rule.
        var organizationProfile = await _organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var modeChatPrompt = RenderModePrompt(mode.ChatSystemPrompt, organizationProfile);

        var chatSystemPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(modeChatPrompt, session.CompanyCallContext);
        chatSystemPrompt = CustomScenarioPromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.CustomScenarioContext);
        // Phase 40.23. Third in the chain and before the organization blocks, for the reason the
        // ordering comment above gives: human-authored data blocks come after template substitution
        // and before the compliance rule, which stays last so nothing can qualify it.
        chatSystemPrompt = AssignmentPracticePromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.AssignmentPracticeContext);
        chatSystemPrompt += OrganizationProfilePromptBuilder.BuildContextBlock(organizationProfile);
        chatSystemPrompt += OrganizationProfilePromptBuilder.BuildPersonaBannedClaimsBlock(organizationProfile);
        var chatResult = await _openAiChatService.SendChatMessageAsync(chatSystemPrompt, session.Messages, cancellationToken);

        var aiMessage = new DialogMessage
        {
            Role = "assistant",
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

        if (!session.Messages.Any(message => message.Role == "user" && !string.IsNullOrWhiteSpace(message.Content)))
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

        // Phase 40.19. Same three steps as the chat prompt, with the evaluation wording of the
        // banned-claims rule: a persona that stays silent while the grader keeps rewarding the rep
        // for saying the forbidden thing teaches it anyway.
        var organizationProfile = await _organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var modeFeedbackPrompt = RenderModePrompt(mode.FeedbackSystemPrompt, organizationProfile);

        var feedbackSystemPrompt = CompanyContextPromptBuilder.BuildFeedbackSystemPrompt(modeFeedbackPrompt, session.CompanyCallContext);
        feedbackSystemPrompt = CustomScenarioPromptBuilder.BuildFeedbackSystemPrompt(feedbackSystemPrompt, session.CustomScenarioContext);
        // Phase 40.23. The grader is told the same thing the character was, so a conversation held
        // against an assignment's persona is judged as that conversation rather than as a generic
        // one — which is what makes the score the threshold reads a score of the assigned work.
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
                // Phase 40.22. The 0-10 grade the learner sees, normalized to the 0-100 scale every
                // score in learning-service is already on, so an assignment's threshold ("оценка
                // >= 70") is comparable without a consumer knowing this service's internal scale.
                Math.Clamp(feedbackResult.Score, 0, 10) * 10),
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
