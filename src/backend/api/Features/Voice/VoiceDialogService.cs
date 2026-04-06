using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Voice;

public class VoiceDialogService : IVoiceDialogService
{
    private readonly AppDbContext _dbContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IOpenAiChatService _openAiService;
    private readonly IElevenLabsService _elevenLabsService;
    private readonly ILogger<VoiceDialogService> _logger;

    public VoiceDialogService(
        AppDbContext dbContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiService,
        IElevenLabsService elevenLabsService,
        ILogger<VoiceDialogService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _openAiService = openAiService;
        _elevenLabsService = elevenLabsService;
        _logger = logger;
    }

    public async Task<Stream> ProcessVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default)
    {
        // Get session
        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(s => s.UserId, userId)
        );
        var session = await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync(ct);

        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        }

        if (session.Status != DialogSessionStatus.Active)
        {
            throw new InvalidOperationException($"Session {sessionId} is not active");
        }

        // Get mode for system prompt and voice settings
        var mode = await _dbContext.DialogModes
            .Include(m => m.Bundle)
            .FirstOrDefaultAsync(m => m.Id == session.ModeId, ct);

        if (mode == null)
        {
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        }

        if (!mode.VoiceEnabled)
        {
            throw new InvalidOperationException($"Voice is not enabled for mode {session.ModeId}");
        }

        // Add user message
        var userMessage = new DialogMessage
        {
            Role = "user",
            Content = transcript,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        };
        session.Messages.Add(userMessage);

        // Generate AI response
        var chatResult = await _openAiService.SendChatMessageAsync(mode.ChatSystemPrompt, session.Messages);

        var aiMessage = new DialogMessage
        {
            Role = "assistant",
            Content = chatResult.Content,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = chatResult.IsStopSignal
        };
        session.Messages.Add(aiMessage);

        // Save messages to MongoDB
        var updateDefinition = Builders<DialogSession>.Update.Set(s => s.Messages, session.Messages);
        await _mongoContext.DialogSessions.UpdateOneAsync(filter, updateDefinition, cancellationToken: ct);

        _logger.LogInformation(
            "Voice message processed for session {SessionId}, user message: {UserLen} chars, AI response: {AiLen} chars",
            sessionId, transcript.Length, chatResult.Content.Length);

        // Generate TTS audio
        var voiceId = mode.VoiceId; // null will use default voice
        var audioStream = await _elevenLabsService.SynthesizeSpeechAsync(chatResult.Content, voiceId, ct);

        return audioStream;
    }
}
