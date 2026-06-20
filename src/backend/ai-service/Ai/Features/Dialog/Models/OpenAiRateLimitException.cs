namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class OpenAiRateLimitException(string message) : Exception(message);
