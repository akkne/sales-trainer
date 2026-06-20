namespace Sellevate.Ai.Features.Voice.Models;

public sealed class GoogleTtsRateLimitException(string message) : GoogleTtsException(message);
