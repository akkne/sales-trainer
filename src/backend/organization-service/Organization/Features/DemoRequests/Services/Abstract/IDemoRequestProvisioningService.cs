using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Features.DemoRequests.Services.Abstract;

/// <summary>
/// Turns one demo-request lead into a working organization with a working administrator, in
/// one call. Deliberately its own service rather than a new case inside <see cref="IDemoRequestService
/// .UpdateStatusAsync"/>: routing provisioning through that method would fire the plain-approval
/// «Заявку одобрили» email on every provision, and that email's whole point is to be the notice sent
/// when nobody has provisioned yet — see <c>DemoRequestNotificationComposer</c> and docs/DECISIONS.md.
/// </summary>
public interface IDemoRequestProvisioningService
{
    /// <summary>
    /// <see langword="null"/> when <paramref name="demoRequestId"/> names no lead. Every other outcome
    /// — a slug collision, or identity-service failing to bootstrap an administrator — is thrown, not
    /// returned, because each needs a different HTTP status and the controller is what maps exceptions
    /// onto those.
    /// </summary>
    Task<DemoRequestProvisioningResultDto?> ProvisionAsync(
        Guid demoRequestId,
        ProvisionDemoRequestRequestDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
