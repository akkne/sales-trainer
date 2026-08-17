namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>Phase 40.17. A programme version together with its ordered items.</summary>
public record ProgramVersionDto(
    Guid Id,
    int VersionNumber,
    string Status,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<ProgramItemDto> Items
);
