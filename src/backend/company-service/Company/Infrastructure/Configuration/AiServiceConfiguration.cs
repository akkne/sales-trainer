namespace Sellevate.Company.Infrastructure.Configuration;

/// <summary>
/// Where ai-service lives and which of its internal endpoints company-service's four AI features
/// call, bound from the <c>AiService</c> section.
///
/// <para>
/// The paths carry defaults but <see cref="BaseUrl"/> does not, and that asymmetry is deliberate:
/// the paths belong to ai-service's contract and change with a coordinated deployment, whereas the
/// host differs per environment. A missing base URL must fail at startup rather than send every
/// briefing request to a relative address.
/// </para>
/// </summary>
public sealed class AiServiceConfiguration
{
    public const string SectionName = "AiService";

    public required string BaseUrl { get; init; }

    public string BriefingPath { get; init; } = "/ai/companies/briefing";

    public string ParseLogPath { get; init; } = "/ai/companies/parse-log";

    public string PersonaPath { get; init; } = "/ai/companies/persona";

    public string ReadinessPath { get; init; } = "/ai/companies/readiness";
}
