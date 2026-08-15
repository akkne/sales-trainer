using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// The body of <c>POST /auth/invites/{token}/accept</c>. Both fields are used only when the
/// invited address has no account yet; when it already has one the invite adds a membership to the
/// existing user and never touches its password or display name
/// (docs/TENANCY/TENANCY.md §4.3).
/// </summary>
public sealed record AcceptInviteRequestDto(
    [MaxLength(100)] string? DisplayName,
    [MinLength(8), MaxLength(128)] string? Password);
