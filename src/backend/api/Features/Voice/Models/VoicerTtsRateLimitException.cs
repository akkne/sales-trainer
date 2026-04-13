namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoicerTtsRateLimitException(string message) : VoicerTtsException(message);
