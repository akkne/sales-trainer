namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class YandexTtsRateLimitException(string message) : YandexTtsException(message);
