namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoiceConfigDto
{
    public bool Enabled { get; set; }
    public int VadSilenceMs { get; set; }
    public int MaxRecordingSeconds { get; set; }
    public int DailyLimitMinutes { get; set; }
    public int MonthlyLimitMinutes { get; set; }
}
