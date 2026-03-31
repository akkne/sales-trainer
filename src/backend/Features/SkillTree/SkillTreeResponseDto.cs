namespace SalesTrainer.Api.Features.SkillTree;

public record SkillTreeResponseDto(
    IReadOnlyList<SkillTreeNodeDto> SkillNodes,
    int CurrentStreakDayCount,
    int TotalXpAmount,
    int WeeklyXpAmount
);
