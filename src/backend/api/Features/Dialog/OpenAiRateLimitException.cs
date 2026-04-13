namespace SalesTrainer.Api.Features.Dialog;

public sealed class OpenAiRateLimitException(string message) : Exception(message);
