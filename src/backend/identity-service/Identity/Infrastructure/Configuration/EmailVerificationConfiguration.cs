namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// Tuning for the email verification code: how long it is, how long it lives, how many guesses it
/// survives, and how soon another one may be requested. All four are rate-limiting decisions, so they
/// are configuration rather than constants — lowering <c>MaximumVerificationAttempts</c> or raising
/// <c>ResendCooldownSeconds</c> tightens the endpoint without a rebuild.
/// </summary>
public sealed class EmailVerificationConfiguration
{
    public const string SectionName = "EmailVerification";

    public int CodeLength { get; init; } = 6;
    public int CodeLifetimeMinutes { get; init; } = 10;
    public int MaximumVerificationAttempts { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
}
