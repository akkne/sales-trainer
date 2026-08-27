namespace Sellevate.Identity.Features.Organizations.Exceptions;

public enum OrganizationBootstrapRejectionReason
{
    /// <summary><see cref="Models.InternalBootstrapAdministratorRequestDto.ActorUserId"/> does not
    /// name a known platform <c>SuperAdmin</c>. The shared secret in front of this route authorizes
    /// the caller's channel, not the actor it claims to act for.</summary>
    ActorNotAuthorized = 0,
}
