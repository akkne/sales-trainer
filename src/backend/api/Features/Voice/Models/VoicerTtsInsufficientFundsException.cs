namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoicerTtsInsufficientFundsException(string message) : VoicerTtsException(message);
