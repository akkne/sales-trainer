namespace Sellevate.Identity.Infrastructure.Configuration;

public sealed class EmailVerificationConfiguration
{
    public const string SectionName = "EmailVerification";

    public int CodeLength { get; init; } = 6;
    public int CodeLifetimeMinutes { get; init; } = 10;
    public int MaximumVerificationAttempts { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
}
