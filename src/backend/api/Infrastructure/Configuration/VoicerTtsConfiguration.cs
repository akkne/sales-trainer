namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class VoicerTtsConfiguration
{
    public const string SectionName = "VoicerTts";
    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://voiceapi.csv666.ru";
    public string VoiceId { get; init; } = "21m00Tcm4TlvDq8ikWAM";
    public string PublicOwnerId { get; init; } = "default";
    public string Model { get; init; } = "eleven_multilingual_v2";
    public double Stability { get; init; } = 0.5;
    public double SimilarityBoost { get; init; } = 0.75;
    public double Speed { get; init; } = 1.0;
    public int PollIntervalMilliseconds { get; init; } = 500;
    public int MaximumPollAttemptCount { get; init; } = 120;
}
