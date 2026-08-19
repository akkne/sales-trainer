using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Features.DemoRequests.Services.Abstract;

/// <summary>
/// The public "Request a demo" lead-capture pipeline, plus the platform-staff read/update surface over
/// what visitors submitted. Deliberately not tenant-scoped — see <see cref="Models.DemoRequest"/> — so,
/// unlike every other feature in this service, <see cref="SubmitAsync"/> is legitimately reachable by
/// an anonymous caller.
/// </summary>
public interface IDemoRequestService
{
    Task<DemoRequestAcceptedDto> SubmitAsync(
        CreateDemoRequestRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DemoRequestDto>> ListDemoRequestsAsync(CancellationToken cancellationToken = default);

    Task<DemoRequestDto?> UpdateStatusAsync(
        Guid demoRequestId, DemoRequestStatus status, CancellationToken cancellationToken = default);
}
