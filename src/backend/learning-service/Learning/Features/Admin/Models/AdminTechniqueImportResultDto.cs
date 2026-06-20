namespace Sellevate.Learning.Features.Admin;

public sealed record AdminTechniqueImportResultDto(
    int CreatedCount,
    int UpdatedCount,
    int FailedCount,
    string[] Errors);
