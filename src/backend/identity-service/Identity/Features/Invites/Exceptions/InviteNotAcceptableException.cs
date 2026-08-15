namespace Sellevate.Identity.Features.Invites.Exceptions;

public sealed class InviteNotAcceptableException(InviteRejectionReason reason, string message)
    : Exception(message)
{
    public InviteRejectionReason Reason { get; } = reason;
}
