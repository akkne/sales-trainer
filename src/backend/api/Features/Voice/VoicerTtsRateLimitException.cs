namespace SalesTrainer.Api.Features.Voice;

public sealed class VoicerTtsRateLimitException(string message) : VoicerTtsException(message);
