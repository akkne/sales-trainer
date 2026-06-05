using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Dialog.Services.Implementation;
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

        // The model answers with {"reply": "...", "endCall": bool}; the parser
        // extracts the spoken reply incrementally. Text chunks are pushed to the
        // client immediately (live subtitles); audio is synthesized afterwards in
        // a single TTS request — Voicer is a queue-based service (~10-30s per task
        // regardless of text length), so per-sentence synthesis would multiply
        // both latency and cost (each task bills a 500-char minimum).
        var replyParser = new StreamingChatReplyParser();
        var sentenceBuffer = new StringBuilder();
        var spokenSentences = new List<string>();

        await foreach (var delta in _openAiService.StreamChatMessageAsync(mode.ChatSystemPrompt, session.Messages, ct))
        {
            var replyText = replyParser.Push(delta);
            if (replyText.Length == 0) continue;

            sentenceBuffer.Append(replyText);

            // Flush at sentence boundaries so subtitles appear as soon as possible.
            while (TryExtractSentence(sentenceBuffer, out var sentence))
            {
                var cleaned = sentence.Trim();
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                spokenSentences.Add(cleaned);
                yield return new VoiceStreamChunk(cleaned, Array.Empty<byte>(), IsStopSignal: false, IsFinal: false);
            }
        }

        var parseResult = replyParser.Complete();
        if (parseResult.UsedFallback)
        {
            _logger.LogWarning(
                "Chat model ignored the JSON reply contract for session {SessionId}; recovered plain-text reply ({Length} chars)",
                sessionId, parseResult.Reply.Length);
            // Nothing was emitted incrementally — speak the recovered reply.
            sentenceBuffer.Clear();
            sentenceBuffer.Append(parseResult.Reply);
            spokenSentences.Clear();
        }

        // Flush remaining buffer (no terminal punctuation).
        var tailCleaned = sentenceBuffer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tailCleaned))
        {
            spokenSentences.Add(tailCleaned);
            yield return new VoiceStreamChunk(tailCleaned, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: false);
        }

        // Synthesize the whole reply at once. A TTS failure must not kill the
        // stream — the user has already received the text above.
        var speechText = string.Join(" ", spokenSentences);
        if (!string.IsNullOrWhiteSpace(speechText))
        {
            var audio = await TrySynthesizeAsync(speechText, mode.VoiceId, sessionId, ct);
            if (audio is { Length: > 0 })
            {
                yield return new VoiceStreamChunk(string.Empty, audio, IsStopSignal: false, IsFinal: false);
            }
        }

        // Persist the assembled AI message after the stream completes successfully.
        session.Messages.Add(new DialogMessage
        {
            Role = "assistant",
            Content = parseResult.Reply,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = parseResult.EndCall
        });
        await _mongoContext.DialogSessions.UpdateOneAsync(
            filter,
            Builders<DialogSession>.Update.Set(s => s.Messages, session.Messages),
            cancellationToken: ct);

        _logger.LogInformation(
            "Streamed voice message for session {SessionId}: user {UserLen} chars, AI {AiLen} chars, endCall={EndCall}",
            sessionId, transcript.Length, parseResult.Reply.Length, parseResult.EndCall);

        // Sentinel chunk with no audio, signaling end of stream + stop flag.
        yield return new VoiceStreamChunk(string.Empty, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: true);
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

    private async Task<byte[]?> TrySynthesizeAsync(string text, string? voiceId, string sessionId, CancellationToken ct)
    {
        try
        {
            return await SynthesizeAsync(text, voiceId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS synthesis failed for session {SessionId} ({TextLength} chars); reply delivered as text only", sessionId, text.Length);
            return null;
        }
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
