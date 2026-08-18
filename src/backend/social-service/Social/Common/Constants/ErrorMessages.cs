namespace Sellevate.Social.Common.Constants;

/// <summary>
/// The messages this service puts in a response body or in an exception it expects a controller to
/// translate. Held here rather than at the throw site so the wording a customer sees cannot drift
/// between two paths that fail for the same reason — several of these are produced by both the
/// member-facing and the admin-facing controller.
///
/// <para>
/// Chat and friendship messages are deliberately vague about <em>why</em> a conversation or a
/// friendship was not found: the repository filter folds "does not exist", "belongs to another
/// organization" and "you are not a participant" into one answer, and telling them apart would
/// confirm that an id exists somewhere else.
/// </para>
/// </summary>
public static class ErrorMessages
{
    public const string ThreadNotFound = "Thread not found";
    public const string ReplyNotFound = "Reply not found";
    public const string ThreadOrReplyNotFound = "Thread or reply not found";
    public const string AcceptedReplyForbidden = "Only the thread author or an admin can do this";
    public const string ThreadTitleAndBodyRequired = "Title and body are required";
    public const string ReplyBodyRequired = "Body is required";

    public const string PhotoNotFound = "Photo not found";
    public const string PhotoOwnerNotFound = "Owner not found";
    public const string PhotoFilesRequired = "No files were provided";
    public const string PhotoValidationFailed = "One or more photos failed validation";

    public const string TagNotFound = "Tag not found";
    public const string TagNameRequired = "Name is required";
    public const string TagSlugConflict = "A tag with this slug already exists";

    public const string FriendRequestToSelf = "Cannot send a friend request to yourself.";
    public const string FriendRequestTargetNotFound = "Target user not found.";
    public const string FriendRequestAlreadyFriends = "You are already friends with this user.";
    public const string FriendRequestAlreadyExists =
        "A friend request already exists between you and this user.";
    public const string FriendRequestNotFound = "Friend request not found.";
    public const string FriendRequestNotPending = "This friend request is no longer pending.";
    public const string FriendRequestAcceptorMustBeAddressee =
        "Only the addressee can accept a friend request.";
    public const string FriendRequestDeclinerMustBeAddressee =
        "Only the addressee can decline a friend request.";
    public const string FriendRequestCancellerMustBeRequester =
        "Only the requester can cancel a friend request.";
    public const string FriendshipNotFound = "Friendship not found.";
    public const string UserNotFound = "User not found.";

    public const string ConversationNotFound = "Conversation not found.";
    public const string ChatRequiresAcceptedFriendship = "You can only chat with accepted friends.";
}
