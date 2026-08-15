namespace Sellevate.Identity.Features.PlatformAdmin.Models;

public sealed record ImpersonationAuditEntryDto(
    Guid Id,
    Guid ActorUserId,
    string ActorEmail,
    OrganizationReferenceDto Organization,
    string Reason,
    DateTime IssuedAt,
    DateTime ExpiresAt);
