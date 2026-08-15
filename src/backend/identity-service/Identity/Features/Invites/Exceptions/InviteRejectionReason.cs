namespace Sellevate.Identity.Features.Invites.Exceptions;

public enum InviteRejectionReason
{
    /// <summary>Unknown, malformed, tampered with, or belonging to another organization — all
    /// collapsed into one reason so the response never confirms that a token exists elsewhere.</summary>
    NotFound = 0,
    Expired = 1,
    Revoked = 2,
    AlreadyAccepted = 3,
    PasswordRequired = 4
}
