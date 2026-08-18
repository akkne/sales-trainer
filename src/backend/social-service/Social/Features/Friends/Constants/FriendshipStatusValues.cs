namespace Sellevate.Social.Features.Friends.Constants;

/// <summary>
/// The <c>friendshipStatus</c> values the friends API puts on the wire, as documented in
/// docs/FRIENDS.md. The frontend's friendship button switches on these exact strings, so they are a
/// published contract and not an internal label: renaming one changes what a screen renders, which
/// is why they live here rather than inline in the service that computes them.
///
/// <para>
/// This is the projection of <c>FriendshipStatus</c> <em>as seen by one viewer</em> — a single
/// pending row is <see cref="PendingOutgoing"/> to the requester and <see cref="PendingIncoming"/> to
/// the addressee — and <c>Declined</c> deliberately reads as <see cref="None"/>, so a declined
/// request looks like no request at all rather than telling the requester they were refused.
/// </para>
/// </summary>
internal static class FriendshipStatusValues
{
    public const string None = "none";
    public const string PendingOutgoing = "pending_outgoing";
    public const string PendingIncoming = "pending_incoming";
    public const string Friends = "friends";
}
