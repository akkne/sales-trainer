namespace SalesTrainer.Api.Features.Auth.Models;

public sealed record VerifyEmailRequestDto(string Email, string Code);
