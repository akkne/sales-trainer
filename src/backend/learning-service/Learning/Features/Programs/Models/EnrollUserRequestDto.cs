namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. Who to put on the newest published programme version. The organization is not in
/// this body and never will be — it comes from <c>ITenantContext</c>, filled from the
/// gateway-validated header (docs/TENANCY/TENANCY.md §1.3, enforced by
/// <c>scripts/tenancy-boundary-lint.py</c>).
/// </summary>
public record EnrollUserRequestDto(Guid UserId);
