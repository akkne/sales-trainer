namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class GoogleTtsRateLimitException(string message) : GoogleTtsException(message);
