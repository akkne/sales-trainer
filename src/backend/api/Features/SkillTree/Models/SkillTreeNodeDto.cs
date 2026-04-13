namespace SalesTrainer.Api.Features.SkillTree.Models;

public record SkillTreeNodeDto(
    Guid SkillId,
    string Slug,
    string Title,
    string IconName,
    int SortOrder,
    string Status,
    int CompletedLessonCount,
    int TotalLessonCount,
    bool IsLocked
);
