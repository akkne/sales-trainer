using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Helpers;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Overrides;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

internal sealed class DialogService : IDialogService
{
    private readonly AiDbContext _databaseContext;
    private readonly IDialogSessionRepository _sessionRepository;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly IDialogScoringWeightsProvider _scoringWeightsProvider;
    private readonly IDialogEventPublisher _dialogEventPublisher;
    private readonly IScenarioValidationService _scenarioValidationService;
    private readonly ILogger<DialogService> _logger;

    public DialogService(
        AiDbContext databaseContext,
        IDialogSessionRepository sessionRepository,
        IOpenAiChatService openAiChatService,
        IDialogScoringWeightsProvider scoringWeightsProvider,
        IDialogEventPublisher dialogEventPublisher,
        IScenarioValidationService scenarioValidationService,
        ILogger<DialogService> logger)
    {
        _databaseContext = databaseContext;
        _sessionRepository = sessionRepository;
        _openAiChatService = openAiChatService;
        _scoringWeightsProvider = scoringWeightsProvider;
        _dialogEventPublisher = dialogEventPublisher;
        _scenarioValidationService = scenarioValidationService;
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

        var session = new DialogSession
        {
            UserId = userId,
            BundleId = bundleId,
            ModeId = modeId,
            Status = DialogSessionStatus.Active,
            Messages = [],
            CompanyCallContext = companyCallContext,
            CustomScenarioContext = customScenarioContext
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

        var chatSystemPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(mode.ChatSystemPrompt, session.CompanyCallContext);
        chatSystemPrompt = CustomScenarioPromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.CustomScenarioContext);
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

        var feedbackSystemPrompt = CompanyContextPromptBuilder.BuildFeedbackSystemPrompt(mode.FeedbackSystemPrompt, session.CompanyCallContext);
        feedbackSystemPrompt = CustomScenarioPromptBuilder.BuildFeedbackSystemPrompt(feedbackSystemPrompt, session.CustomScenarioContext);
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
                earnedXp),
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
}
