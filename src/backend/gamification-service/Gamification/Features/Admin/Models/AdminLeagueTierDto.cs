namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record AdminLeagueTierDto(
    Guid Id,
    string Key,
    string Name,
    string Color,
    int Order);
