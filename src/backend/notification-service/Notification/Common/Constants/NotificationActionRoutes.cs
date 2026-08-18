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

    /// <summary>
    /// Phase 40.25. A coaching note and a dispute verdict both open the same list — everything said
    /// about the recipient's conversations, with the row in question named. One screen rather than
    /// two, because a manager who has to look in two places will look in neither.
    /// </summary>
    public static string DialogReview(Guid noteId) => $"/dialog-reviews?note={noteId}";

    /// <summary>
    /// Phase 40.26. The digest's action, and the whole difference between «отчёт, который РОП может
    /// открыть» and «адресный пуш с действием»: the link does not open a report, it opens the
    /// assignment with the reminder already named and already scoped to the people the notice just
    /// listed.
    ///
    /// <para>
    /// <b>A deep link rather than a button that acts by itself.</b> A URL in an email that sends
    /// messages to a team the moment it is opened is a URL a mail scanner can fire; the destination
    /// is the РОП's own screen, where one confirmation calls
    /// <c>POST /admin/assignments/{id}/remind?scope=not_started</c> — an endpoint that exists and
    /// answers today. The screen does not (40.20 waits on the owner's design), which is exactly why
    /// the parameters are decided here and now: whoever draws it reads the action out of the link
    /// instead of inventing one.
    /// </para>
    /// </summary>
    public static string AssignmentReminderPrompt(Guid assignmentId)
        => $"/admin/assignments/{assignmentId}?action=remind&scope=not_started";

    /// <summary>
    /// Phase 40.26. A filed dispute opens the РОП's review queue with the row in question named —
    /// the counterpart of <see cref="DialogReview"/>, which opens the manager's own inbox.
    /// </summary>
    public static string AdminDialogReview(Guid noteId) => $"/admin/dialog-reviews?note={noteId}";
}
