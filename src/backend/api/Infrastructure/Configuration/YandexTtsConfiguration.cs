namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class YandexTtsConfiguration
{
    public const string SectionName = "YandexTts";
    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://tts.api.cloud.yandex.net";
    public string Voice { get; init; } = "marina";
    public string Lang { get; init; } = "ru-RU";
    public string? Role { get; init; }
    public string? Speed { get; init; }
    public string? FolderId { get; init; }
    public int SampleRateHertz { get; init; } = 48000;
}
