namespace SalesTrainer.Api.Features.Auth.Models;

public sealed record RegistrationResultDto(string Email, bool RequiresEmailVerification);
