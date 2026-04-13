namespace SalesTrainer.Api.Features.Voice;

public sealed class VoicerTtsTimeoutException(string message) : VoicerTtsException(message);
