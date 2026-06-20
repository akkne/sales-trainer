namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record UpdateLeagueTierRequestDto(
    string Name,
    string Color,
    int Order);
