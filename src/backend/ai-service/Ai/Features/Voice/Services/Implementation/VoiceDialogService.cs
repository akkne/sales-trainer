using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

internal sealed class VoiceDialogService : IVoiceDialogService
{
    private readonly AiDbContext _dbContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IOpenAiChatService _openAiService;
    private readonly ITtsRouter _ttsRouter;
    private readonly ILogger<VoiceDialogService> _logger;

    public VoiceDialogService(
        AiDbContext dbContext,
        MongoDbContext mongoContext,
        IOpenAiChatService openAiService,
        ITtsRouter ttsRouter,
        ILogger<VoiceDialogService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _openAiService = openAiService;
        _ttsRouter = ttsRouter;
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

        var userMsg = new DialogMessage
        {
            Role = "user",
            Content = transcript,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        };
        session.Messages.Add(userMsg);
        // AI7a: Push the single user message instead of overwriting the whole array.
        await _mongoContext.DialogSessions.UpdateOneAsync(
            filter,
            Builders<DialogSession>.Update.Push(s => s.Messages, userMsg),
            cancellationToken: ct);

        var replyParser = new StreamingChatReplyParser();
        var sentenceChunker = new SentenceChunker();
        // TTS pipeline: synthesis of sentence N runs concurrently with LLM streaming of sentence N+1.
        // Tasks are awaited in order, so audio chunks always arrive in reply order.
        var pendingAudio = new Queue<Task<byte[]?>>();

        await foreach (var delta in _openAiService.StreamChatMessageAsync(mode.ChatSystemPrompt, session.Messages, ct))
        {
            var replyText = replyParser.Push(delta);
            if (replyText.Length == 0) continue;

            sentenceChunker.Append(replyText);

            while (sentenceChunker.TryExtractSentence(out var sentence))
            {
                var cleaned = sentence.Trim();
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                yield return new VoiceStreamChunk(cleaned, Array.Empty<byte>(), IsStopSignal: false, IsFinal: false);
                pendingAudio.Enqueue(TrySynthesizeAsync(cleaned, mode.VoiceId, sessionId, ct));
            }

            // Flush audio that is already synthesized without blocking the LLM stream.
            while (pendingAudio.Count > 0 && pendingAudio.Peek().IsCompleted)
            {
                var readyAudio = await pendingAudio.Dequeue();
                if (readyAudio is { Length: > 0 })
                    yield return new VoiceStreamChunk(string.Empty, readyAudio, IsStopSignal: false, IsFinal: false);
            }
        }

        var parseResult = replyParser.Complete();
        if (parseResult.UsedFallback)
        {
            _logger.LogWarning(
                "Chat model ignored the JSON reply contract for session {SessionId}; recovered plain-text reply ({Length} chars)",
                sessionId, parseResult.Reply.Length);
            sentenceChunker.Replace(parseResult.Reply);
        }

        var tailCleaned = sentenceChunker.DrainRemaining().Trim();
        if (!string.IsNullOrWhiteSpace(tailCleaned))
        {
            yield return new VoiceStreamChunk(tailCleaned, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: false);
            pendingAudio.Enqueue(TrySynthesizeAsync(tailCleaned, mode.VoiceId, sessionId, ct));
        }

        while (pendingAudio.Count > 0)
        {
            var audio = await pendingAudio.Dequeue();
            if (audio is { Length: > 0 })
                yield return new VoiceStreamChunk(string.Empty, audio, IsStopSignal: false, IsFinal: false);
        }

        var assistantMsg = new DialogMessage
        {
            Role = "assistant",
            Content = parseResult.Reply,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = parseResult.EndCall
        };
        session.Messages.Add(assistantMsg);
        // AI7a: Push the single assistant message instead of overwriting the whole array.
        await _mongoContext.DialogSessions.UpdateOneAsync(
            filter,
            Builders<DialogSession>.Update.Push(s => s.Messages, assistantMsg),
            cancellationToken: ct);

        _logger.LogInformation(
            "Streamed voice message for session {SessionId}: user {UserLen} chars, AI {AiLen} chars, endCall={EndCall}",
            sessionId, transcript.Length, parseResult.Reply.Length, parseResult.EndCall);

        yield return new VoiceStreamChunk(string.Empty, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: true);
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
        var stream = await _ttsRouter.SynthesizeSpeechAsync(text, voiceId, ct);
        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
    }
}
