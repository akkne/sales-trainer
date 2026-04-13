namespace SalesTrainer.Api.Features.Dialog;

public sealed class OpenAiPaymentRequiredException(string message) : Exception(message);
