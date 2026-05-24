using System.Runtime.CompilerServices;
using System.Text;
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

    public async IAsyncEnumerable<VoiceStreamChunk> StreamVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(s => s.UserId, userId)
        );
        var session = await _mongoContext.DialogSessions.Find(filter).FirstOrDefaultAsync(ct);

        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        if (session.Status != DialogSessionStatus.Active)
            throw new InvalidOperationException($"Session {sessionId} is not active");

        var mode = await _dbContext.DialogModes
            .Include(m => m.Bundle)
            .FirstOrDefaultAsync(m => m.Id == session.ModeId, ct);

        if (mode == null)
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        if (!mode.VoiceEnabled)
            throw new InvalidOperationException($"Voice is not enabled for mode {session.ModeId}");

        // Record user message immediately so it's persisted even if the stream is cancelled mid-flight.
        session.Messages.Add(new DialogMessage
        {
            Role = "user",
            Content = transcript,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        });
        await _mongoContext.DialogSessions.UpdateOneAsync(
            filter,
            Builders<DialogSession>.Update.Set(s => s.Messages, session.Messages),
            cancellationToken: ct);

        var fullResponseBuilder = new StringBuilder();
        var sentenceBuffer = new StringBuilder();
        var hasStopSignal = false;

        await foreach (var delta in _openAiService.StreamChatMessageAsync(mode.ChatSystemPrompt, session.Messages, ct))
        {
            fullResponseBuilder.Append(delta);
            sentenceBuffer.Append(delta);

            // Flush at sentence boundaries to start TTS as early as possible.
            while (TryExtractSentence(sentenceBuffer, out var sentence))
            {
                var cleaned = sentence.Replace("[DIALOG_END]", "").Trim();
                if (sentence.Contains("[DIALOG_END]")) hasStopSignal = true;
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                var audio = await SynthesizeAsync(cleaned, mode.VoiceId, ct);
                yield return new VoiceStreamChunk(cleaned, audio, IsStopSignal: false, IsFinal: false);
            }
        }

        // Flush remaining buffer (no terminal punctuation).
        var tail = sentenceBuffer.ToString();
        if (tail.Contains("[DIALOG_END]")) hasStopSignal = true;
        var tailCleaned = tail.Replace("[DIALOG_END]", "").Trim();
        if (!string.IsNullOrWhiteSpace(tailCleaned))
        {
            var audio = await SynthesizeAsync(tailCleaned, mode.VoiceId, ct);
            yield return new VoiceStreamChunk(tailCleaned, audio, IsStopSignal: hasStopSignal, IsFinal: false);
        }

        // Persist the assembled AI message after the stream completes successfully.
        var fullText = fullResponseBuilder.ToString();
        var finalText = fullText.Replace("[DIALOG_END]", "").Trim();
        session.Messages.Add(new DialogMessage
        {
            Role = "assistant",
            Content = finalText,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = hasStopSignal
        });
        await _mongoContext.DialogSessions.UpdateOneAsync(
            filter,
            Builders<DialogSession>.Update.Set(s => s.Messages, session.Messages),
            cancellationToken: ct);

        _logger.LogInformation(
            "Streamed voice message for session {SessionId}: user {UserLen} chars, AI {AiLen} chars, stop={Stop}",
            sessionId, transcript.Length, finalText.Length, hasStopSignal);

        // Sentinel chunk with no audio, signaling end of stream + stop flag.
        yield return new VoiceStreamChunk(string.Empty, Array.Empty<byte>(), IsStopSignal: hasStopSignal, IsFinal: true);
    }

    private static bool TryExtractSentence(StringBuilder buffer, out string sentence)
    {
        // Emit on . ! ? \n boundaries, but only after at least ~20 chars to avoid
        // synthesizing single-word fragments. Skip abbreviations isn't perfect —
        // good-enough heuristic for sales-dialog Russian.
        const int minLength = 20;
        var text = buffer.ToString();
        if (text.Length < minLength)
        {
            sentence = string.Empty;
            return false;
        }

        var idx = -1;
        for (var i = minLength; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '.' || ch == '!' || ch == '?' || ch == '\n')
            {
                idx = i;
                break;
            }
        }
        if (idx < 0)
        {
            sentence = string.Empty;
            return false;
        }

        sentence = text[..(idx + 1)];
        buffer.Remove(0, idx + 1);
        return true;
    }

    private async Task<byte[]> SynthesizeAsync(string text, string? voiceId, CancellationToken ct)
    {
        var provider = (_configuration["Voice:TtsProvider"] ?? "voicer").Trim().ToLowerInvariant();
        Stream stream = provider switch
        {
            "google" when _googleTtsService.IsConfigured =>
                await _googleTtsService.SynthesizeSpeechAsync(text, voiceId, ct),
            "voicer" when _voicerTtsService.IsConfigured =>
                await _voicerTtsService.SynthesizeSpeechAsync(text, voiceId, ct),
            _ when _voicerTtsService.IsConfigured =>
                await _voicerTtsService.SynthesizeSpeechAsync(text, voiceId, ct),
            _ when _googleTtsService.IsConfigured =>
                await _googleTtsService.SynthesizeSpeechAsync(text, voiceId, ct),
            _ => throw new InvalidOperationException(
                $"TTS provider '{provider}' is not configured. Set Voice:TtsProvider and the matching API key.")
        };

        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
    }
}
