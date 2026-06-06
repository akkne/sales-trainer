namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class TtsRouterConfiguration
{
    public const string SectionName = "Voice";

    public string TtsProvider { get; init; } = "yandex";
}
