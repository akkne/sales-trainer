namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class OpenAiPaymentRequiredException(string message) : Exception(message);
