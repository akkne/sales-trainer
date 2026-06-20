namespace Sellevate.Ai.Features.Voice.Models;

public sealed class YandexTtsRateLimitException(string message) : YandexTtsException(message);
