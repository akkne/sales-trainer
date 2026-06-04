namespace SalesTrainer.Api.Features.Voice.Models;

/// <summary>Per-user voice minute spend, aggregated from MongoDB dialog sessions.</summary>
public sealed class AdminVoiceUsageEntryDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DailyUsedSeconds { get; set; }
    public int MonthlyUsedSeconds { get; set; }
    public int TotalSeconds { get; set; }
    public int SessionCount { get; set; }
    public DateTime? LastCallAt { get; set; }
}

public sealed class AdminVoiceUsageDto
{
    public int DailyLimitSeconds { get; set; }
    public int MonthlyLimitSeconds { get; set; }
    public List<AdminVoiceUsageEntryDto> Users { get; set; } = [];
}
