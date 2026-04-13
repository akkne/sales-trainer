namespace SalesTrainer.Api.Features.Auth.Models;

public sealed record RegisterRequestDto(string Email, string Password, string DisplayName);
