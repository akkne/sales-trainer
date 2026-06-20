namespace Sellevate.Learning.Features.SkillTree.Models;

public record SkillTreeResponseDto(
    IReadOnlyList<SkillTreeNodeDto> SkillNodes,
    int CurrentStreakDayCount,
    int TotalXpAmount,
    int WeeklyXpAmount,
    int DailyXpAmount,
    int DailyXpGoal,
    int WeeklyXpGoal
);
