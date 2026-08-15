namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// An organization named in a response, shaped like organization-service's
/// <c>OrganizationSummaryDto</c>: the identifier is a plain <c>Id</c> because it is the id *of*
/// this object, not a tenant scope being asserted. Keeping outbound organization identifiers in
/// this shape is what lets <c>scripts/tenancy-boundary-lint.py</c> stay strict about the inbound
/// ones without an ever-growing list of exceptions.
/// </summary>
public sealed record OrganizationReferenceDto(Guid Id, string Name);
