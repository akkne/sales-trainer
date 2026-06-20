namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class TtsRouterConfiguration
{
    public const string SectionName = "Voice";

    public string TtsProvider { get; init; } = "yandex";
}
