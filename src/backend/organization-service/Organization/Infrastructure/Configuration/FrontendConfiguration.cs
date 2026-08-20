namespace Sellevate.Organization.Infrastructure.Configuration;

/// <summary>
/// The base address of the Next.js frontend. Read here so <c>DemoRequestNotificationComposer</c> can
/// build an absolute registration link for the approval email without hardcoding a host; every other
/// consumer of this same <c>Frontend:Url</c> value (the gateway's CORS allow-list, the other backend
/// services) reads it independently, so this is a second reader of one already-shared configuration
/// key, not a new one.
/// </summary>
public sealed class FrontendConfiguration
{
    public const string SectionName = "Frontend";

    public string Url { get; init; } = "http://localhost:3000";

    /// <summary>
    /// The first entry of <see cref="Url"/>, which is what a link in an email has to use.
    ///
    /// <para>
    /// <b>This is not defensive paranoia — <c>Frontend:Url</c> genuinely holds a comma-separated
    /// list.</b> Its original and still primary consumer is the CORS allow-list, which needs every
    /// permitted origin, so <c>Program.cs</c> splits it on commas and `docker-compose.yml` ships
    /// <c>http://localhost:3000,https://sellevate.vercel.app</c> as the default. Interpolating the raw
    /// value into a URL produces <c>http://localhost:3000,https://sellevate.vercel.app/register</c> —
    /// a link that is broken everywhere except a single-origin local setup, which is exactly the
    /// setup where nobody would notice.
    /// </para>
    ///
    /// <para>
    /// First-wins rather than last-wins because the list reads primary-origin-first in every
    /// environment file we ship.
    /// </para>
    /// </summary>
    public string PrimaryUrl =>
        Url.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
        ?? "http://localhost:3000";
}
