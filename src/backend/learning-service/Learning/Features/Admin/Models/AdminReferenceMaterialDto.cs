namespace Sellevate.Learning.Features.Admin;

public record AdminReferenceMaterialDto(
    Guid Id,
    Guid SkillId,
    string SkillTitle,
    string Title,
    string MarkdownContent,
    int SortOrder,
    string? Category,
    string[] Tags
);
