namespace SalesTrainer.Api.Features.Auth;

public record RegisterRequestDto(string Email, string Password, string DisplayName);
