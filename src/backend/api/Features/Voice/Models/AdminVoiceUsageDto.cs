namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class AdminVoiceUsageDto
{
    public int DailyLimitSeconds { get; set; }
    public int MonthlyLimitSeconds { get; set; }
    public List<AdminVoiceUsageEntryDto> Users { get; set; } = [];
}
