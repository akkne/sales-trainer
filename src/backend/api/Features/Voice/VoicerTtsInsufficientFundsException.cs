namespace SalesTrainer.Api.Features.Voice;

public sealed class VoicerTtsInsufficientFundsException(string message) : VoicerTtsException(message);
