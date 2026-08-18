namespace Sellevate.Identity.Features.PlatformAdmin.Exceptions;

public sealed class PlatformAdminOperationException(PlatformAdminRejectionReason reason, string message)
    : Exception(message)
{
    public PlatformAdminRejectionReason Reason { get; } = reason;
}
