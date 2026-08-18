namespace Sellevate.Ai.Features.Voice.Models;

public sealed class AdminVoiceUsageDto
{
    /// <summary>The per-user allowance (<c>Voice:DailyLimitMinutes</c>), unchanged since the feature shipped.</summary>
    public int DailyLimitSeconds { get; set; }

    public int MonthlyLimitSeconds { get; set; }

    /// <summary>
    /// Phase 40.33. The organization-wide voice allowance and what is left of it. Added because the
    /// per-user numbers above answer «кто много говорит» and never answered «сколько осталось у
    /// компании» — which is the number that decides whether the next call connects.
    /// </summary>
    public int OrganizationDailyLimitSeconds { get; set; }

    public int OrganizationMonthlyLimitSeconds { get; set; }

    public int OrganizationUsedSecondsToday { get; set; }

    public int OrganizationUsedSecondsThisMonth { get; set; }

    public List<AdminVoiceUsageEntryDto> Users { get; set; } = [];
}
