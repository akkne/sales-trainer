namespace Sellevate.Ai.Features.Quotas.Services.Implementation;

/// <summary>
/// Phase 40.33. The organization-wide voice counter's key shape, in one place because two things
/// read it: the meter that reserves against it and the spend report that renders it.
///
/// <para>
/// It follows 40.11's rule — every ai-service Redis key is namespaced <c>org:{organizationId}:</c> —
/// one level deeper than the per-user counter <c>VoiceUsageService</c> has kept since the voice
/// feature shipped. <c>voice:org:</c> cannot collide with <c>voice:{userId}:</c> because a user id is
/// a GUID and never the literal <c>org</c>.
/// </para>
/// </summary>
internal static class AiVoiceQuotaKeys
{
    public static string Day(Guid organizationId, DateTime now) =>
        $"org:{organizationId}:voice:org:day:{now.Year}:{now.Month}:{now.Day}";

    public static string Month(Guid organizationId, DateTime now) =>
        $"org:{organizationId}:voice:org:month:{now.Year}:{now.Month}";

    public static long DayExpiryUnix(DateTime now) =>
        (long)(new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1) - DateTime.UnixEpoch)
        .TotalSeconds;

    public static long MonthExpiryUnix(DateTime now) =>
        (long)(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1) - DateTime.UnixEpoch)
        .TotalSeconds;
}
