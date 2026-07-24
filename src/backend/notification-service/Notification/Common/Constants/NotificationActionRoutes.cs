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
}
