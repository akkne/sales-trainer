namespace SalesTrainer.Api.Features.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt
);
