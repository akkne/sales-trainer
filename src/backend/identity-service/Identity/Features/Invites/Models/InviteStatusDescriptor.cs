namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// The four states an invite can be in, and the single rule that decides which one a row is in.
///
/// <para>
/// The state is derived rather than stored: the row carries <c>AcceptedAt</c>, <c>RevokedAt</c> and
/// <c>ExpiresAt</c>, and any column duplicating what those three already say would eventually
/// disagree with them. Deriving it on the server also keeps the clock that decides "expired" on one
/// machine rather than on every browser.
/// </para>
///
/// <para>
/// Order matters and is the whole content of the rule: acceptance and revocation are facts recorded
/// on the row and outrank the clock, so an invite accepted last month whose expiry has since passed
/// still reads <see cref="Accepted"/>, not <see cref="Expired"/>.
/// </para>
/// </summary>
public static class InviteStatusDescriptor
{
    public const string Pending = "pending";

    public const string Accepted = "accepted";

    public const string Revoked = "revoked";

    public const string Expired = "expired";

    public static string Describe(Invite invite, DateTime asOf)
    {
        ArgumentNullException.ThrowIfNull(invite);

        if (invite.AcceptedAt is not null)
        {
            return Accepted;
        }

        if (invite.RevokedAt is not null)
        {
            return Revoked;
        }

        return invite.ExpiresAt <= asOf ? Expired : Pending;
    }
}
