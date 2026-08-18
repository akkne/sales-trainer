namespace Sellevate.Identity.Features.Invites.Constants;

/// <summary>
/// Wire values of the invite feature. The <c>*Reason</c> constants are machine-readable codes returned
/// per rejected address in <c>CreateInvitesResponseDto</c> and are read by the frontend, so they are a
/// contract rather than prose — the <c>*Message</c> constants are the human half.
/// </summary>
public static class InviteConstants
{
    public const string EmailSubject = "Приглашение в Sellevate";

    public const string NotFoundMessage = "Invite not found.";
    public const string ExpiredMessage = "This invite has expired.";
    public const string RevokedMessage = "This invite has been revoked.";
    public const string AlreadyAcceptedMessage = "This invite has already been used.";
    public const string PasswordRequiredMessage = "A password is required to create the account.";

    public const string InvalidEmailReason = "invalid-email";
    public const string DuplicateInRequestReason = "duplicate-in-request";
    public const string AlreadyMemberReason = "already-a-member";
    public const string AlreadyInvitedReason = "invite-already-pending";
}
