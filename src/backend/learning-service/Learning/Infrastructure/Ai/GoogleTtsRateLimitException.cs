namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class GoogleTtsRateLimitException(string message) : GoogleTtsException(message);
