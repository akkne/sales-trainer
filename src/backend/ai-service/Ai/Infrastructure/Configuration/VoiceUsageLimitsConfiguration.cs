namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// <b>Bound but resolved nowhere.</b> Registered by <c>VoiceServiceCollectionExtensions</c> and injected
/// by nothing; the per-user voice allowances that are actually enforced come from
/// <see cref="VoiceFeatureConfiguration"/>, and the organization-wide ones from
/// <c>AiQuotaConfiguration</c>. It binds the same <c>Voice</c> section as
/// <see cref="VoiceFeatureConfiguration"/> while defaulting both limits to 0 rather than 30/300, so
/// anything that started injecting it would silently read "window disabled". Recorded in
/// <c>docs/CONFIGURATION.md</c> as a key that does nothing; deleting it also means deleting the
/// registration.
/// </summary>
public sealed class VoiceUsageLimitsConfiguration
{
    public const string SectionName = "Voice";

    public int DailyLimitMinutes { get; init; } = 0;
    public int MonthlyLimitMinutes { get; init; } = 0;
}
