namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoicerTtsTimeoutException(string message) : VoicerTtsException(message);
