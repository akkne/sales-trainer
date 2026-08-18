using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Configuration;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Data;
using StackExchange.Redis;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Runs an <c>ai_dialogue</c> exercise: keeps the practice transcript, enforces the turn limit, and
/// delivers the partner's reply either as text or as a voice stream.
///
/// <para>
/// <b>The transcript lives only in Redis, never in Postgres.</b> A practice conversation is working
/// state, so it expires on its own (<see cref="ExerciseDialogOptions.ChatStateTtlHours"/>) rather than
/// being cleaned up by anything. The consequence a caller must accept: a Redis outage loses the
/// conversation and the next turn starts a fresh one — the turn is answered rather than failed, which
/// is the right trade for a training exercise but would not be for graded work.
/// </para>
///
/// <para>
/// <b>Turns are counted from the transcript, not tracked in a counter.</b> Both transports derive the
/// turn number by counting the learner's messages, so the two cannot disagree about how far a
/// conversation has got, and a redelivered request cannot inflate it.
/// </para>
///
/// <para>
/// <b>Every AI failure degrades to a reply; only client cancellation propagates.</b> A missing
/// provider, a rejected request or an unreachable host all produce the canned reply, because ending a
/// practice conversation with an error mid-sentence teaches nothing. Text-to-speech failure is
/// narrower still — the text has already been delivered, so the learner reads what they cannot hear.
/// </para>
/// </summary>
internal sealed class ExerciseDialogService : IExerciseDialogService
{
    /// <summary>
    /// Roles inside the cached transcript. Serialized into Redis and read back by later turns, so an
    /// existing conversation would break if a value changed mid-flight.
    /// </summary>
    private const string LearnerRole = "user";

    /// <inheritdoc cref="LearnerRole"/>
    private const string AiPartnerRole = "assistant";

    /// <summary>
    /// Segment identifying this cache's keys under the tenant prefix. Changing it orphans every
    /// in-flight conversation until the old keys expire.
    /// </summary>
    private const string ChatStateKeySegment = "exercise_chat";

    /// <summary>
    /// Sent when the AI partner cannot be reached. Deliberately neutral and in-character: the learner
    /// carries on practising rather than being told the system is broken.
    /// </summary>
    private const string FallbackAiReply = "Понял вас. Что ещё вы хотели бы обсудить?";

    private const string TurnLimitReachedReply =
        "Диалог завершён — достигнуто максимальное количество реплик.";

    /// <summary>
    /// Word that ends the conversation in the no-provider fallback path only.
    /// </summary>
    private const string FallbackCompletionKeyword = "спасибо";

    private readonly LearningDbContext _databaseContext;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly ITtsRouter _ttsRouter;
    private readonly ILogger<ExerciseDialogService> _logger;
    private readonly IDatabase _redis;
    private readonly ITenantContext _tenantContext;
    private readonly ExerciseDialogOptions _options;

    public ExerciseDialogService(
        LearningDbContext databaseContext,
        IOpenAiChatService openAiChatService,
        ITtsRouter ttsRouter,
        ILogger<ExerciseDialogService> logger,
        IConnectionMultiplexer redisConnection,
        ITenantContext tenantContext,
        IOptions<ExerciseDialogOptions> options)
    {
        _databaseContext = databaseContext;
        _openAiChatService = openAiChatService;
        _ttsRouter = ttsRouter;
        _logger = logger;
        _redis = redisConnection.GetDatabase();
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    private TimeSpan ChatStateTtl => TimeSpan.FromHours(_options.ChatStateTtlHours);

    /// <summary>
    /// Throws if the exercise does not exist or is not an <c>ai_dialogue</c>. Exists so the streaming
    /// endpoint can fail with a real status code before it commits a 200, reusing exactly the lookup the
    /// stream itself would perform rather than a second, drifting copy of the same check.
    /// </summary>
    public async Task ValidateExerciseForVoiceAsync(Guid exerciseId, CancellationToken cancellationToken = default)
    {
        await BuildExerciseChatContextAsync(exerciseId, cancellationToken);
    }

    public async Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var chatContext = await BuildExerciseChatContextAsync(exerciseId, cancellationToken);
        var cacheKey = BuildChatCacheKey(userId, exerciseId);
        var messages = await GetChatMessagesFromCacheAsync(cacheKey);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new ExerciseChatResponseDto(
                Response: string.Empty,
                IsComplete: false,
                IsFinished: false,
                TurnNumber: messages.Count(message => message.Role == LearnerRole),
                MaxTurns: chatContext.MaxTurns);
        }

        messages.Add(new ChatMessage(LearnerRole, userMessage));

        var turnNumber = messages.Count(message => message.Role == LearnerRole);
        if (turnNumber > chatContext.MaxTurns)
        {
            await SaveChatMessagesToCacheAsync(cacheKey, messages);
            return new ExerciseChatResponseDto(
                Response: TurnLimitReachedReply,
                IsComplete: true,
                IsFinished: false,
                TurnNumber: turnNumber,
                MaxTurns: chatContext.MaxTurns);
        }

        var dialogHistory = ToDialogHistory(messages);
        var aiResponse = await GenerateAiResponseAsync(chatContext.SystemPrompt, dialogHistory, cancellationToken);
        messages.Add(new ChatMessage(AiPartnerRole, aiResponse.Response));

        await SaveChatMessagesToCacheAsync(cacheKey, messages);

        return new ExerciseChatResponseDto(
            Response: aiResponse.Response,
            IsComplete: aiResponse.IsComplete || turnNumber >= chatContext.MaxTurns,
            IsFinished: aiResponse.IsFinished,
            TurnNumber: turnNumber,
            MaxTurns: chatContext.MaxTurns);
    }

    public async IAsyncEnumerable<VoiceStreamChunk> StreamExerciseVoiceAsync(
        Guid userId,
        Guid exerciseId,
        string transcript,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatContext = await BuildExerciseChatContextAsync(exerciseId, cancellationToken);
        var cacheKey = BuildChatCacheKey(userId, exerciseId);
        var messages = await GetChatMessagesFromCacheAsync(cacheKey);

        if (!string.IsNullOrWhiteSpace(transcript))
            messages.Add(new ChatMessage(LearnerRole, transcript));

        var dialogHistory = ToDialogHistory(messages);

        var replyParser = new StreamingChatReplyParser();
        var sentenceChunker = new SentenceChunker();
        var pendingAudio = new Queue<Task<byte[]?>>();

        await foreach (var delta in _openAiChatService.StreamChatMessageAsync(chatContext.SystemPrompt, dialogHistory, cancellationToken))
        {
            var replyText = replyParser.Push(delta);
            if (replyText.Length == 0) continue;

            sentenceChunker.Append(replyText);

            while (sentenceChunker.TryExtractSentence(out var sentence))
            {
                var cleaned = sentence.Trim();
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                yield return new VoiceStreamChunk(cleaned, Array.Empty<byte>(), IsStopSignal: false, IsFinal: false);
                pendingAudio.Enqueue(TrySynthesizeAsync(cleaned, cancellationToken));
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
            sentenceChunker.Replace(parseResult.Reply);

        var tailCleaned = sentenceChunker.DrainRemaining().Trim();
        if (!string.IsNullOrWhiteSpace(tailCleaned))
        {
            yield return new VoiceStreamChunk(tailCleaned, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: false);
            pendingAudio.Enqueue(TrySynthesizeAsync(tailCleaned, cancellationToken));
        }

        while (pendingAudio.Count > 0)
        {
            var audio = await pendingAudio.Dequeue();
            if (audio is { Length: > 0 })
                yield return new VoiceStreamChunk(string.Empty, audio, IsStopSignal: false, IsFinal: false);
        }

        messages.Add(new ChatMessage(AiPartnerRole, parseResult.Reply));
        await SaveChatMessagesToCacheAsync(cacheKey, messages);

        var maxTurnsReached = messages.Count(message => message.Role == LearnerRole) >= chatContext.MaxTurns;
        yield return new VoiceStreamChunk(
            string.Empty,
            Array.Empty<byte>(),
            IsStopSignal: parseResult.EndCall || maxTurnsReached,
            IsFinal: true);
    }

    private async Task<byte[]?> TrySynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await _ttsRouter.SynthesizeSpeechAsync(text, null, cancellationToken);
            await using (stream)
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                return memoryStream.ToArray();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Exercise TTS synthesis failed ({TextLength} chars); reply delivered as text only", text.Length);
            return null;
        }
    }

    private async Task<ExerciseChatContext> BuildExerciseChatContextAsync(
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        Exercise exercise;
        await using (await TenantTransactionScope.BeginReadAsync(_databaseContext, cancellationToken))
        {
            exercise = await _databaseContext.Exercises
                .FirstOrDefaultAsync(candidate => candidate.Id == exerciseId, cancellationToken)
                ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");
        }

        if (exercise.Type != ExerciseTypes.AiDialogue)
            throw new NotSupportedException("Chat is only supported for ai_dialogue exercises.");

        var content = JsonDocument.Parse(exercise.SerializedContent).RootElement;
        var maximumTurns = content.TryGetProperty(ExerciseContentFields.MaximumTurns, out var maximumTurnsElement)
            ? maximumTurnsElement.GetInt32()
            : _options.DefaultMaximumTurns;

        var persona = content.TryGetProperty(ExerciseContentFields.Persona, out var personaElement)
            ? personaElement.GetString() ?? ""
            : "";
        var scenario = content.TryGetProperty(ExerciseContentFields.Scenario, out var scenarioElement)
            ? scenarioElement.GetString() ?? ""
            : "";
        var authoredContext = content.TryGetProperty(ExerciseContentFields.Context, out var contextElement)
            ? contextElement.GetString() ?? ""
            : "";
        var aiPrompt = content.TryGetProperty(ExerciseContentFields.AiPrompt, out var aiPromptElement)
            ? aiPromptElement.GetString() ?? ""
            : "";

        var systemPrompt = !string.IsNullOrEmpty(aiPrompt)
            ? aiPrompt
            : $"Ты играешь роль: {persona}. Сценарий: {scenario}. {authoredContext}\n\nОтвечай кратко, в 1-3 предложения. Веди себя естественно для своей роли. Пользователь звонит первым.";

        return new ExerciseChatContext(systemPrompt, maximumTurns);
    }

    /// <summary>
    /// Phase 40.14. This key holds the full transcript of a practice dialogue — real tenant data —
    /// and was the last one in the backend without an <c>org:{orgId}:</c> prefix, after 40.11
    /// prefixed ai-service's verdict cache, voice counters and idempotency store and 40.13 did the
    /// same for notification inboxes and analytics presence.
    ///
    /// <para>
    /// Nothing was leaking through it: both components are globally unique GUIDs, so no two
    /// organizations could ever collide on a key. What the prefix buys is the two properties that
    /// come from the naming scheme rather than from the values — "no learning-service key is shared
    /// across organizations" stays checkable by reading key names, and offboarding a customer stays
    /// a single <c>SCAN org:{orgId}:*</c> instead of a sweep that silently leaves every practice
    /// transcript behind. It also stops a person moved between organizations from carrying a warm
    /// cache across the boundary with them.
    /// </para>
    ///
    /// <para>
    /// Old keys are never read again and expire on their own
    /// (<see cref="ExerciseDialogOptions.ChatStateTtlHours"/>),
    /// so nothing has to be migrated or flushed — the Redis instance is shared with every other
    /// service. The only user-visible effect is that a practice dialogue in flight across the deploy
    /// restarts from its first turn.
    /// </para>
    /// </summary>
    private string BuildChatCacheKey(Guid userId, Guid exerciseId)
    {
        var organizationId = _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        return $"org:{organizationId}:{ChatStateKeySegment}:{userId}:{exerciseId}";
    }

    private static List<DialogMessage> ToDialogHistory(IEnumerable<ChatMessage> messages) =>
        messages.Select(message => new DialogMessage
        {
            Role = message.Role,
            Content = message.Content,
            Timestamp = DateTime.UtcNow
        }).ToList();

    private async Task<List<ChatMessage>> GetChatMessagesFromCacheAsync(string cacheKey)
    {
        try
        {
            var json = await _redis.StringGetAsync(cacheKey);
            if (json.HasValue)
                return JsonSerializer.Deserialize<List<ChatMessage>>(json!) ?? [];
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis read failed for key {CacheKey}; starting fresh dialog", cacheKey);
        }
        return [];
    }

    private async Task SaveChatMessagesToCacheAsync(string cacheKey, List<ChatMessage> messages)
    {
        try
        {
            var json = JsonSerializer.Serialize(messages);
            await _redis.StringSetAsync(cacheKey, json, ChatStateTtl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis write failed for key {CacheKey}; dialog state will not persist", cacheKey);
        }
    }

    private async Task<AiChatResponse> GenerateAiResponseAsync(
        string systemPrompt,
        List<DialogMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!_openAiChatService.IsConfigured)
        {
            _logger.LogWarning("OpenAI service is not configured, using fallback response");
            var isComplete = messages.Count(message => message.Role == LearnerRole) >= _options.FallbackCompletionTurnThreshold
                             && messages.LastOrDefault()?.Content
                                 .Contains(FallbackCompletionKeyword, StringComparison.OrdinalIgnoreCase) == true;

            return new AiChatResponse(
                Response: FallbackAiReply,
                IsComplete: isComplete,
                IsFinished: false);
        }

        try
        {
            var result = await _openAiChatService.SendChatMessageAsync(systemPrompt, messages, cancellationToken);
            return new AiChatResponse(
                Response: result.Content,
                IsComplete: result.IsStopSignal,
                IsFinished: result.IsStopSignal);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is OpenAiException or HttpRequestException)
        {
            _logger.LogWarning(exception, "AI provider unavailable for chat, using fallback response");
            return new AiChatResponse(
                Response: FallbackAiReply,
                IsComplete: false,
                IsFinished: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate AI response for chat, using fallback");
            return new AiChatResponse(
                Response: FallbackAiReply,
                IsComplete: false,
                IsFinished: false);
        }
    }

    private sealed record ExerciseChatContext(string SystemPrompt, int MaxTurns);

    private record ChatMessage(string Role, string Content);

    private record AiChatResponse(string Response, bool IsComplete, bool IsFinished);
}
