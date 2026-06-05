namespace SalesTrainer.Api.Features.Voice.Models;

public class GoogleTtsRateLimitException(string message) : GoogleTtsException(message);
