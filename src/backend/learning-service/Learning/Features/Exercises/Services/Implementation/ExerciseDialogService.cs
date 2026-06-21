using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;
using StackExchange.Redis;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ExerciseDialogService : IExerciseDialogService
{
    private readonly LearningDbContext _databaseContext;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly ITtsRouter _ttsRouter;
    private readonly ILogger<ExerciseDialogService> _logger;
    private readonly IDatabase _redis;

    // TTL for dialog state: 24 hours is enough for any single practice session.
    private static readonly TimeSpan ChatStateTtl = TimeSpan.FromHours(24);

    public ExerciseDialogService(
        LearningDbContext databaseContext,
        IOpenAiChatService openAiChatService,
        ITtsRouter ttsRouter,
        ILogger<ExerciseDialogService> logger,
        IConnectionMultiplexer redisConnection)
    {
        _databaseContext = databaseContext;
        _openAiChatService = openAiChatService;
        _ttsRouter = ttsRouter;
        _logger = logger;
        _redis = redisConnection.GetDatabase();
    }

    public async Task ValidateExerciseForVoiceAsync(Guid exerciseId, CancellationToken cancellationToken = default)
    {
        // Reuse the same DB lookup used by the stream; throws on missing/wrong type.
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
                TurnNumber: messages.Count(m => m.Role == "user"),
                MaxTurns: chatContext.MaxTurns);
        }

        messages.Add(new ChatMessage("user", userMessage));

        var turnNumber = messages.Count(m => m.Role == "user");
        if (turnNumber > chatContext.MaxTurns)
        {
            await SaveChatMessagesToCacheAsync(cacheKey, messages);
            return new ExerciseChatResponseDto(
                Response: "Диалог завершён — достигнуто максимальное количество реплик.",
                IsComplete: true,
                IsFinished: false,
                TurnNumber: turnNumber,
                MaxTurns: chatContext.MaxTurns);
        }

        var dialogHistory = ToDialogHistory(messages);
        var aiResponse = await GenerateAiResponseAsync(chatContext.SystemPrompt, dialogHistory, cancellationToken);
        messages.Add(new ChatMessage("assistant", aiResponse.Response));

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
            messages.Add(new ChatMessage("user", transcript));

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

        messages.Add(new ChatMessage("assistant", parseResult.Reply));
        await SaveChatMessagesToCacheAsync(cacheKey, messages);

        var maxTurnsReached = messages.Count(m => m.Role == "user") >= chatContext.MaxTurns;
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
            _logger.LogError(exception, "Exercise TTS synthesis failed ({TextLength} chars); reply delivered as text only", text.Length);
            return null;
        }
    }

    private async Task<ExerciseChatContext> BuildExerciseChatContextAsync(
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        var exercise = await _databaseContext.Exercises
            .FirstOrDefaultAsync(e => e.Id == exerciseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        if (exercise.Type != ExerciseTypes.AiDialogue)
            throw new NotSupportedException("Chat is only supported for ai_dialogue exercises.");

        var content = JsonDocument.Parse(exercise.SerializedContent).RootElement;
        var maxTurns = content.TryGetProperty("max_turns", out var maxEl) ? maxEl.GetInt32() : 10;

        var persona = content.TryGetProperty("persona", out var personaEl) ? personaEl.GetString() ?? "" : "";
        var scenario = content.TryGetProperty("scenario", out var scenarioEl) ? scenarioEl.GetString() ?? "" : "";
        var contextInfo = content.TryGetProperty("context", out var contextEl) ? contextEl.GetString() ?? "" : "";
        var aiPrompt = content.TryGetProperty("ai_prompt", out var aiPromptEl) ? aiPromptEl.GetString() ?? "" : "";

        var systemPrompt = !string.IsNullOrEmpty(aiPrompt)
            ? aiPrompt
            : $"Ты играешь роль: {persona}. Сценарий: {scenario}. {contextInfo}\n\nОтвечай кратко, в 1-3 предложения. Веди себя естественно для своей роли. Пользователь звонит первым.";

        return new ExerciseChatContext(systemPrompt, maxTurns);
    }

    private static string BuildChatCacheKey(Guid userId, Guid exerciseId) =>
        $"exercise_chat:{userId}:{exerciseId}";

    private static List<DialogMessage> ToDialogHistory(IEnumerable<ChatMessage> messages) =>
        messages.Select(m => new DialogMessage
        {
            Role = m.Role,
            Content = m.Content,
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for key {CacheKey}; starting fresh dialog", cacheKey);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for key {CacheKey}; dialog state will not persist", cacheKey);
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
            var isComplete = messages.Count(m => m.Role == "user") >= 3 &&
                             messages.LastOrDefault()?.Content.Contains("спасибо", StringComparison.OrdinalIgnoreCase) == true;

            return new AiChatResponse(
                Response: "Понял вас. Что ещё вы хотели бы обсудить?",
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate AI response for chat, using fallback");
            return new AiChatResponse(
                Response: "Понял вас. Что ещё вы хотели бы обсудить?",
                IsComplete: false,
                IsFinished: false);
        }
    }

    private sealed record ExerciseChatContext(string SystemPrompt, int MaxTurns);

    private record ChatMessage(string Role, string Content);

    private record AiChatResponse(string Response, bool IsComplete, bool IsFinished);
}
