namespace Sellevate.Learning.Features.Reference.Models;

public record ReferenceMaterialDto(
    Guid MaterialId,
    string Title,
    string MarkdownContent,
    int SortOrder,
    string? Category,
    string[] Tags,
    Guid SkillId
);
