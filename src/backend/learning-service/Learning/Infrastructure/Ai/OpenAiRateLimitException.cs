namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class OpenAiRateLimitException(string message) : Exception(message);
