using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Dialog;

public class DialogService
{
    private readonly AppDbContext _dbContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IOpenAiChatService _openAiService;
    private readonly ILogger<DialogService> _logger;

    public DialogService(
        AppDbContext dbContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiService,
        ILogger<DialogService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _openAiService = openAiService;
        _logger = logger;
    }

    public bool IsOpenAiConfigured => _openAiService.IsConfigured;

    public async Task<List<DialogBundle>> GetActiveBundlesAsync()
    {
        return await _dbContext.DialogBundles
            .Include(bundle => bundle.Skill)
            .Where(bundle => bundle.IsActive)
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync();
    }

    public async Task<DialogBundle?> GetBundleByIdAsync(Guid bundleId)
    {
        return await _dbContext.DialogBundles
            .Include(bundle => bundle.Skill)
            .FirstOrDefaultAsync(bundle => bundle.Id == bundleId);
    }

    public async Task<List<DialogMode>> GetActiveModesForBundleAsync(Guid bundleId)
    {
        return await _dbContext.DialogModes
            .Where(mode => mode.BundleId == bundleId && mode.IsActive)
            .OrderBy(mode => mode.SortOrder)
            .ToListAsync();
    }

    public async Task<DialogMode?> GetModeByIdAsync(Guid modeId)
    {
        return await _dbContext.DialogModes
            .Include(mode => mode.Bundle)
            .FirstOrDefaultAsync(mode => mode.Id == modeId);
    }

    public async Task<DialogSession> StartSessionAsync(Guid userId, Guid bundleId, Guid modeId)
    {
        var mode = await GetModeByIdAsync(modeId);
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

        await _mongoContext.DialogSessions.InsertOneAsync(session);
        _logger.LogInformation("Started dialog session {SessionId} for user {UserId}", session.Id, userId);

        return session;
    }

    public async Task<DialogSession?> GetSessionByIdAsync(string sessionId)
    {
        var filter = Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId);
        return await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<DialogSession?> GetSessionForUserAsync(string sessionId, Guid userId)
    {
        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId)
        );
        return await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<DialogSession>> GetUserSessionsAsync(Guid userId)
    {
        var filter = Builders<DialogSession>.Filter.Eq(session => session.UserId, userId);
        var sort = Builders<DialogSession>.Sort.Descending(session => session.CreatedAt);
        return await _mongoContext.DialogSessions.Find(filter).Sort(sort).ToListAsync();
    }

    public async Task<DialogMessage> SendMessageAsync(string sessionId, Guid userId, string userMessageContent)
    {
        var session = await GetSessionForUserAsync(sessionId, userId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        }

        if (session.Status != DialogSessionStatus.Active)
        {
            throw new InvalidOperationException($"Session {sessionId} is not active");
        }

        var mode = await GetModeByIdAsync(session.ModeId);
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

        var chatResult = await _openAiService.SendChatMessageAsync(mode.ChatSystemPrompt, session.Messages);

        var aiMessage = new DialogMessage
        {
            Role = "assistant",
            Content = chatResult.Content,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = chatResult.IsStopSignal
        };

        session.Messages.Add(aiMessage);

        var updateDefinition = Builders<DialogSession>.Update.Set(s => s.Messages, session.Messages);
        await _mongoContext.DialogSessions.UpdateOneAsync(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            updateDefinition
        );

        _logger.LogInformation("Added message to session {SessionId}, total messages: {Count}", sessionId, session.Messages.Count);

        return aiMessage;
    }

    public async Task<DialogFeedbackResult> CompleteSessionAsync(string sessionId, Guid userId)
    {
        var session = await GetSessionForUserAsync(sessionId, userId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        }

        if (session.Status != DialogSessionStatus.Active)
        {
            throw new InvalidOperationException($"Session {sessionId} is not active");
        }

        var mode = await GetModeByIdAsync(session.ModeId);
        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        }

        var feedbackResult = await _openAiService.GenerateFeedbackAsync(mode.FeedbackSystemPrompt, session.Messages);

        var feedback = new DialogFeedback
        {
            Summary = feedbackResult.Summary,
            Content = feedbackResult.Content,
            GeneratedAt = DateTime.UtcNow
        };

        var updateDefinition = Builders<DialogSession>.Update
            .Set(s => s.Status, DialogSessionStatus.Completed)
            .Set(s => s.Feedback, feedback)
            .Set(s => s.XpEarned, feedbackResult.XpReward)
            .Set(s => s.CompletedAt, DateTime.UtcNow);

        await _mongoContext.DialogSessions.UpdateOneAsync(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            updateDefinition
        );

        _logger.LogInformation("Completed session {SessionId} for user {UserId}, XP earned: {Xp}", sessionId, userId, feedbackResult.XpReward);

        return new DialogFeedbackResult
        {
            Feedback = feedback,
            XpEarned = feedbackResult.XpReward
        };
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, Guid userId)
    {
        var session = await GetSessionForUserAsync(sessionId, userId);
        if (session == null)
        {
            return false;
        }

        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(s => s.UserId, userId)
        );

        var result = await _mongoContext.DialogSessions.DeleteOneAsync(filter);
        _logger.LogInformation("Deleted session {SessionId} for user {UserId}", sessionId, userId);

        return result.DeletedCount > 0;
    }
}

public class DialogFeedbackResult
{
    public DialogFeedback Feedback { get; set; } = null!;
    public int XpEarned { get; set; }
}
