namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class OpenAiPaymentRequiredException(string message) : Exception(message);
