namespace Sellevate.Notification.Common.Constants;

public static class NotificationActionRoutes
{
    public const string FriendRequests = "/friends?tab=requests";
    public const string Profile = "/profile";
    public const string Home = "/";

    public static string FriendProfile(Guid friendUserId) => $"/friends/{friendUserId}";

    public static string ChatConversation(Guid conversationId) => $"/friends/chat/{conversationId}";

    public static string DiscussThread(Guid threadId) => $"/discuss/{threadId}";

    public static string CompanyDetails(Guid companyId) => $"/companies/{companyId}";

    /// <summary>
    /// Phase 40.23. An assignment has no screen of its own: the roadmap's requirement is that the
    /// active assignment <em>is</em> the first thing the manager sees, so its surface is the card at
    /// the top of the learning path. Sending the notification anywhere else would contradict the
    /// feature it announces. A dedicated screen arrives with the РОП's dashboard (40.25); the query
    /// parameter is carried now so that link keeps working when it does.
    /// </summary>
    public static string Assignment(Guid assignmentId) => $"/tree?assignment={assignmentId}";
}
