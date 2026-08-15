namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// The platform staff member behind a superadmin action, as read from their validated token by
/// the controller. Passed explicitly rather than resolved from an ambient accessor so the service
/// is testable without an <c>HttpContext</c>.
/// </summary>
/// <param name="IsAlreadyImpersonating">
/// True when the caller's own token was itself minted by the impersonation endpoint. Such a token
/// carries <c>role: User</c> and would already fail <c>RequireSuperAdmin</c>; the flag is the
/// belt-and-braces check so the rule survives a future policy edit.
/// </param>
public sealed record PlatformAdminActor(
    Guid UserId,
    string Email,
    string DisplayName,
    bool IsAlreadyImpersonating);
