namespace Sellevate.Social.Features.Friends.Constants;

/// <summary>
/// The <c>direction</c> values on a pending friend request, relative to the user who asked for the
/// list: the same row is <see cref="Incoming"/> to its addressee and <see cref="Outgoing"/> to its
/// requester. Published to the frontend, which uses it to decide between Accept/Decline and Cancel.
/// </summary>
internal static class FriendRequestDirections
{
    public const string Incoming = "incoming";
    public const string Outgoing = "outgoing";
}
