namespace Sellevate.Ai.Infrastructure.Configuration;

public sealed class WhisperConfiguration
{
    public const string SectionName = "Whisper";

    public string Model { get; init; } = "whisper-1";
    public string ApiUrl { get; init; } = "https://api.openai.com/v1/audio/transcriptions";
    public string? Language { get; init; }
    public int MaximumFileSizeMegabytes { get; init; } = 25;
    public long MaximumFileSizeBytes => (long)MaximumFileSizeMegabytes * 1024 * 1024;
    public int RequestSizeLimitBytes { get; init; } = 50 * 1024 * 1024;
}
