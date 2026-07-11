namespace Sellevate.Company.Infrastructure.Configuration;

public sealed class AiServiceConfiguration
{
    public const string SectionName = "AiService";

    public required string BaseUrl { get; init; }

    public string BriefingPath { get; init; } = "/ai/companies/briefing";

    public string ParseLogPath { get; init; } = "/ai/companies/parse-log";

    public string PersonaPath { get; init; } = "/ai/companies/persona";

    public string ReadinessPath { get; init; } = "/ai/companies/readiness";
}
