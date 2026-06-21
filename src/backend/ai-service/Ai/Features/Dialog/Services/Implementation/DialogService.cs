using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;

namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

internal sealed class DialogService : IDialogService
{
    private readonly AiDbContext _databaseContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly IDialogScoringWeightsProvider _scoringWeightsProvider;
    private readonly IDialogEventPublisher _dialogEventPublisher;
    private readonly ILogger<DialogService> _logger;

    public DialogService(
        AiDbContext databaseContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiChatService,
        IDialogScoringWeightsProvider scoringWeightsProvider,
        IDialogEventPublisher dialogEventPublisher,
        ILogger<DialogService> logger)
    {
        _databaseContext = databaseContext;
        _mongoContext = mongoContext;
        _openAiChatService = openAiChatService;
        _scoringWeightsProvider = scoringWeightsProvider;
        _dialogEventPublisher = dialogEventPublisher;
        _logger = logger;
    }

    public bool IsOpenAiConfigured => _openAiChatService.IsConfigured;

    public async Task<List<DialogBundle>> GetActiveBundlesAsync(CancellationToken cancellationToken = default)
    {
        return await _databaseContext.DialogBundles
            .Where(bundle => bundle.IsActive)
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
        return await _databaseContext.DialogModes
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

    public async Task<DialogSession> StartSessionAsync(
        Guid userId,
        Guid bundleId,
        Guid modeId,
        CancellationToken cancellationToken = default)
    {
        var mode = await GetModeByIdAsync(modeId, cancellationToken);
        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {modeId} not found");
        }

        var session = new DialogSession
        {
            UserId = userId,
            BundleId = bundleId,
            ModeId = modeId,
            Status = DialogSessionStatus.Active,
            Messages = []
        };

        await _mongoContext.DialogSessions.InsertOneAsync(session, cancellationToken: cancellationToken);
        _logger.LogInformation("Started dialog session {SessionId} for user {UserId}", session.Id, userId);

        return session;
    }

    public async Task<DialogSession?> GetSessionByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId);
        return await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DialogSession?> GetSessionForUserAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId)
        );
        return await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<DialogSession>> GetUserSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.Eq(session => session.UserId, userId);
        var sort = Builders<DialogSession>.Sort.Descending(session => session.CreatedAt);
        return await _mongoContext.DialogSessions.Find(filter).Sort(sort).ToListAsync(cancellationToken);
    }

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

        var chatResult = await _openAiChatService.SendChatMessageAsync(mode.ChatSystemPrompt, session.Messages, cancellationToken);

        var aiMessage = new DialogMessage
        {
            Role = "assistant",
            Content = chatResult.Content,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = chatResult.IsStopSignal
        };

        session.Messages.Add(aiMessage);

        // AI7a: PushEach appends only the new messages — avoids lost-update when concurrent reads
        // hold a stale Messages list and then overwrite the whole array with Set.
        var updateDefinition = Builders<DialogSession>.Update.PushEach(
            sessionRecord => sessionRecord.Messages,
            new[] { userMessage, aiMessage });
        await _mongoContext.DialogSessions.UpdateOneAsync(
            Builders<DialogSession>.Filter.Eq(sessionRecord => sessionRecord.Id, sessionId),
            updateDefinition,
            cancellationToken: cancellationToken
        );

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
            await _mongoContext.DialogSessions.UpdateOneAsync(
                Builders<DialogSession>.Filter.Eq(sessionRecord => sessionRecord.Id, sessionId),
                Builders<DialogSession>.Update
                    .Set(sessionRecord => sessionRecord.Status, DialogSessionStatus.Abandoned)
                    .Set(sessionRecord => sessionRecord.XpEarned, 0)
                    .Set(sessionRecord => sessionRecord.CompletedAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);

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

        var feedbackResult = await _openAiChatService.GenerateFeedbackAsync(mode.FeedbackSystemPrompt, session.Messages, xpWeights, cancellationToken);

        var earnedXp = (int)Math.Round(feedbackResult.XpReward * scoringWeights.Multiplier);

        var feedback = new DialogFeedback
        {
            Summary = feedbackResult.Summary,
            Content = feedbackResult.Content,
            GeneratedAt = DateTime.UtcNow
        };

        var updateDefinition = Builders<DialogSession>.Update
            .Set(sessionRecord => sessionRecord.Status, DialogSessionStatus.Completed)
            .Set(sessionRecord => sessionRecord.Feedback, feedback)
            .Set(sessionRecord => sessionRecord.XpEarned, earnedXp)
            .Set(sessionRecord => sessionRecord.CompletedAt, DateTime.UtcNow);

        await _mongoContext.DialogSessions.UpdateOneAsync(
            Builders<DialogSession>.Filter.Eq(sessionRecord => sessionRecord.Id, sessionId),
            updateDefinition,
            cancellationToken: cancellationToken
        );

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
        var session = await GetSessionForUserAsync(sessionId, userId, cancellationToken);
        if (session == null)
        {
            return false;
        }

        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(sessionRecord => sessionRecord.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(sessionRecord => sessionRecord.UserId, userId)
        );

        var result = await _mongoContext.DialogSessions.DeleteOneAsync(filter, cancellationToken);
        _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);

        return result.DeletedCount > 0;
    }
}
