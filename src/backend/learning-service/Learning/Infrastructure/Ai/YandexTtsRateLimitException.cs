namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class YandexTtsRateLimitException(string message) : YandexTtsException(message);
