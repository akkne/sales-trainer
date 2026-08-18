namespace Sellevate.Notification.Common.Constants;

public static class ErrorMessages
{
    public const string NotificationNotFound = "Notification not found.";

    /// <summary>
    /// Deliberately word-for-word identical to the message <c>TenantSaveChangesInterceptor</c> and
    /// every other tenant guard in the codebase raises, so an operator greps one phrase to find any
    /// unset-tenant failure regardless of which service produced it. Changing the wording here
    /// silently breaks that, and the tenancy tests assert on this exact text.
    /// </summary>
    public const string OrganizationContextNotSet = "Organization context is not set.";
}
