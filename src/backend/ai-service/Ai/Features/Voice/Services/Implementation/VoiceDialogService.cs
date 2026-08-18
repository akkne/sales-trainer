using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Helpers;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Turns one learner utterance into an interleaved stream of reply text and synthesized audio.
///
/// <para>
/// <b>Text is yielded before its audio, and synthesis runs ahead of playback.</b> Each extracted
/// sentence is emitted immediately and its synthesis queued; completed audio is drained whenever the
/// head of the queue is ready. That ordering is what makes the character start speaking while the
/// model is still writing — awaiting each synthesis inline would serialise the two and roughly double
/// the wait before the first sound.
/// </para>
///
/// <para>
/// <b>A failed synthesis degrades to text.</b> Only cancellation propagates; anything else is logged
/// and the turn continues silently, because a learner reading the reply is a degraded call and a torn
/// stream is a broken one.
/// </para>
///
/// <para>
/// Both messages are persisted, so a voice turn is replayable and gradable exactly like a typed one —
/// including <c>IsStopSignal</c>, which is how the character hanging up survives a page reload.
/// </para>
///
/// <para>
/// Phase 40.23. A voice turn is graded by the same completion path as a typed one and counts towards
/// the same assignment threshold, so it has to meet the same character: the assignment persona is
/// spliced in here too. The context was resolved and frozen when the session started; this path only
/// reads it.
/// </para>
/// </summary>
internal sealed class VoiceDialogService : IVoiceDialogService
{
    private readonly AiDbContext _dbContext;
    private readonly IDialogSessionRepository _sessionRepository;
    private readonly IOpenAiChatService _openAiService;
    private readonly ITtsRouter _ttsRouter;
    private readonly ILogger<VoiceDialogService> _logger;

    public VoiceDialogService(
        AiDbContext dbContext,
        IDialogSessionRepository sessionRepository,
        IOpenAiChatService openAiService,
        ITtsRouter ttsRouter,
        ILogger<VoiceDialogService> logger)
    {
        _dbContext = dbContext;
        _sessionRepository = sessionRepository;
        _openAiService = openAiService;
        _ttsRouter = ttsRouter;
        _logger = logger;
    }

    public async IAsyncEnumerable<VoiceStreamChunk> StreamVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.FindForUserAsync(sessionId, userId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found for user {userId}");
        if (session.Status != DialogSessionStatus.Active)
            throw new InvalidOperationException($"Session {sessionId} is not active");

        var mode = await _dbContext.DialogModes
            .Include(dialogMode => dialogMode.Bundle)
            .FirstOrDefaultAsync(dialogMode => dialogMode.Id == session.ModeId, cancellationToken);

        if (mode == null)
            throw new InvalidOperationException($"Mode {session.ModeId} not found");
        if (!mode.VoiceEnabled)
            throw new InvalidOperationException($"Voice is not enabled for mode {session.ModeId}");

        var userMessage = new DialogMessage
        {
            Role = DialogMessageRoles.User,
            Content = transcript,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = false
        };
        session.Messages.Add(userMessage);
        await _sessionRepository.AppendMessagesAsync(sessionId, userId, [userMessage], cancellationToken);

        var chatSystemPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(mode.ChatSystemPrompt, session.CompanyCallContext);
        chatSystemPrompt = AssignmentPracticePromptBuilder.BuildChatSystemPrompt(chatSystemPrompt, session.AssignmentPracticeContext);

        var replyParser = new StreamingChatReplyParser();
        var sentenceChunker = new SentenceChunker();
        var pendingAudio = new Queue<Task<byte[]?>>();

        await foreach (var delta in _openAiService.StreamChatMessageAsync(chatSystemPrompt, session.Messages, cancellationToken))
        {
            var replyText = replyParser.Push(delta);
            if (replyText.Length == 0) continue;

            sentenceChunker.Append(replyText);

            while (sentenceChunker.TryExtractSentence(out var sentence))
            {
                var cleanedSentence = sentence.Trim();
                if (string.IsNullOrWhiteSpace(cleanedSentence)) continue;

                yield return new VoiceStreamChunk(cleanedSentence, Array.Empty<byte>(), IsStopSignal: false, IsFinal: false);
                pendingAudio.Enqueue(TrySynthesizeAsync(cleanedSentence, mode.VoiceId, sessionId, cancellationToken));
            }

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

        var cleanedTail = sentenceChunker.DrainRemaining().Trim();
        if (!string.IsNullOrWhiteSpace(cleanedTail))
        {
            yield return new VoiceStreamChunk(cleanedTail, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: false);
            pendingAudio.Enqueue(TrySynthesizeAsync(cleanedTail, mode.VoiceId, sessionId, cancellationToken));
        }

        while (pendingAudio.Count > 0)
        {
            var audio = await pendingAudio.Dequeue();
            if (audio is { Length: > 0 })
                yield return new VoiceStreamChunk(string.Empty, audio, IsStopSignal: false, IsFinal: false);
        }

        var assistantMessage = new DialogMessage
        {
            Role = DialogMessageRoles.Assistant,
            Content = parseResult.Reply,
            Timestamp = DateTime.UtcNow,
            IsStopSignal = parseResult.EndCall
        };
        session.Messages.Add(assistantMessage);
        await _sessionRepository.AppendMessagesAsync(sessionId, userId, [assistantMessage], cancellationToken);

        _logger.LogInformation(
            "Streamed voice message for session {SessionId}: user {UserLen} chars, AI {AiLen} chars, endCall={EndCall}, endCallReason={EndCallReason}",
            sessionId, transcript.Length, parseResult.Reply.Length, parseResult.EndCall, parseResult.EndCallReason ?? "unspecified");

        yield return new VoiceStreamChunk(string.Empty, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: true);
    }

    /// <summary>
    /// Synthesis that never breaks the turn. Cancellation propagates; any other failure yields
    /// <see langword="null"/> so the reply is delivered as text only.
    /// </summary>
    private async Task<byte[]?> TrySynthesizeAsync(string text, string? voiceId, string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            return await SynthesizeAsync(text, voiceId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "TTS synthesis failed for session {SessionId} ({TextLength} chars); reply delivered as text only", sessionId, text.Length);
            return null;
        }
    }

    private async Task<byte[]> SynthesizeAsync(string text, string? voiceId, CancellationToken cancellationToken)
    {
        var audioStream = await _ttsRouter.SynthesizeSpeechAsync(text, voiceId, cancellationToken);
        await using (audioStream)
        {
            using var buffer = new MemoryStream();
            await audioStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
    }
}
