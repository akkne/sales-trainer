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

    /// <summary>
    /// The master switch for proving an address by code, off by default
    /// (<c>EMAIL_VERIFICATION_ENABLED</c>).
    ///
    /// <para>
    /// It governs both ends of the same rule, so the two can never disagree: with it on,
    /// <c>POST /auth/register</c> creates an unverified account and mails a code instead of
    /// issuing tokens, and <c>POST /auth/login</c> refuses that account until the code is
    /// entered. With it off, sign-up marks the address verified on the spot and no code is ever
    /// generated.
    /// </para>
    ///
    /// <para>
    /// Off is the deliberate default rather than a convenience: in local dev MailerSend is
    /// unconfigured and codes only reach the log, so a mandatory code makes sign-up untestable
    /// without reading container output. It also costs little today — a self-registered account
    /// holds no membership and can reach nothing until an organization invites that exact
    /// address, which proves the mailbox anyway (docs/EMAIL_VERIFICATION.md).
    /// </para>
    /// </summary>
    public bool Enabled { get; init; }

    public int CodeLength { get; init; } = 6;
    public int CodeLifetimeMinutes { get; init; } = 10;
    public int MaximumVerificationAttempts { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
}
