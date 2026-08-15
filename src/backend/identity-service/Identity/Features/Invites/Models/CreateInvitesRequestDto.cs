using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// One request shape covers both the single invite and the bulk paste-a-list case: a РОП
/// onboarding forty managers must not click forty times (docs/TENANCY/TENANCY.md §4.3).
/// <see cref="Email"/> is sugar for a one-element <see cref="Emails"/>; at least one of the two
/// must be present.
///
/// <para>
/// There is deliberately no organization field. The target organization comes from the
/// gateway-validated <c>X-Organization-Id</c> header only (docs/TENANCY/TENANCY.md §1.3), which is
/// also what <c>scripts/tenancy-boundary-lint.py</c> enforces on this file.
/// </para>
/// </summary>
public sealed record CreateInvitesRequestDto(
    [EmailAddress] string? Email,
    IReadOnlyList<string>? Emails,
    [Required] string Role);
