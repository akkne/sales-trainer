namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class GoogleTtsConfiguration
{
    public const string SectionName = "GoogleTts";

    public required string ApiKey { get; init; }
    public string VoiceName { get; init; } = "ru-RU-Wavenet-A";
    public string LanguageCode { get; init; } = "ru-RU";
    public double SpeakingRate { get; init; } = 1.0;
    public double Pitch { get; init; } = 0.0;
}
