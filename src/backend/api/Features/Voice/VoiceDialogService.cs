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
    private readonly IVoicerTtsService _voicerTtsService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly ILogger<VoiceDialogService> _logger;

    public VoiceDialogService(
        AppDbContext dbContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiService,
        IVoicerTtsService voicerTtsService,
        IGoogleTtsService googleTtsService,
        ILogger<VoiceDialogService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _openAiService = openAiService;
        _voicerTtsService = voicerTtsService;
        _googleTtsService = googleTtsService;
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

        // Generate TTS audio - prefer Google TTS, fallback to VoicerTTS
        Stream audioStream;
        if (_googleTtsService.IsConfigured)
        {
            var voiceName = mode.VoiceId; // Can be used for Google voice name
            audioStream = await _googleTtsService.SynthesizeSpeechAsync(chatResult.Content, voiceName, ct);
        }
        else if (_voicerTtsService.IsConfigured)
        {
            var voiceId = mode.VoiceId;
            audioStream = await _voicerTtsService.SynthesizeSpeechAsync(chatResult.Content, voiceId, ct);
        }
        else
        {
            throw new InvalidOperationException("No TTS service is configured");
        }

        return audioStream;
    }
}
