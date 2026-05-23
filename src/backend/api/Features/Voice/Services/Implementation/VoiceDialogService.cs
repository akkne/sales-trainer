using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

public class VoiceDialogService : IVoiceDialogService
{
    private readonly AppDbContext _dbContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IOpenAiChatService _openAiService;
    private readonly IVoicerTtsService _voicerTtsService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoiceDialogService> _logger;

    public VoiceDialogService(
        AppDbContext dbContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiService,
        IVoicerTtsService voicerTtsService,
        IGoogleTtsService googleTtsService,
        IConfiguration configuration,
        ILogger<VoiceDialogService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _openAiService = openAiService;
        _voicerTtsService = voicerTtsService;
        _googleTtsService = googleTtsService;
        _configuration = configuration;
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

        // Pick TTS provider from config: Voice:TtsProvider = "voicer" (default, RUB-friendly) | "google"
        var provider = (_configuration["Voice:TtsProvider"] ?? "voicer").Trim().ToLowerInvariant();
        Stream audioStream = provider switch
        {
            "google" when _googleTtsService.IsConfigured =>
                await _googleTtsService.SynthesizeSpeechAsync(chatResult.Content, mode.VoiceId, ct),
            "voicer" when _voicerTtsService.IsConfigured =>
                await _voicerTtsService.SynthesizeSpeechAsync(chatResult.Content, mode.VoiceId, ct),
            _ when _voicerTtsService.IsConfigured =>
                await _voicerTtsService.SynthesizeSpeechAsync(chatResult.Content, mode.VoiceId, ct),
            _ when _googleTtsService.IsConfigured =>
                await _googleTtsService.SynthesizeSpeechAsync(chatResult.Content, mode.VoiceId, ct),
            _ => throw new InvalidOperationException(
                $"TTS provider '{provider}' is not configured. Set Voice:TtsProvider and the matching API key.")
        };

        return audioStream;
    }
}
