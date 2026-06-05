namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class GoogleTtsAuthenticationException(string message) : GoogleTtsException(message);
