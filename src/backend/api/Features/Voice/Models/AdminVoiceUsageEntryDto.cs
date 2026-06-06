namespace SalesTrainer.Api.Features.Voice.Models;

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
