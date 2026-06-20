namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record CreateLeagueTierRequestDto(
    string Key,
    string Name,
    string Color,
    int Order);
