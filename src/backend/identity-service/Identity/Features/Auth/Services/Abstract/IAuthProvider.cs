using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

/// <summary>
/// One way of proving an identity. The seam Phase 40.8 exists to create: today there is exactly
/// one implementation, <c>PasswordAuthProvider</c>, and that is the whole point — when a customer
/// requires sign-in through their own directory, the work is adding a provider rather than
/// rewriting login, session issuance, invites and provisioning at once
/// (docs/TENANCY/TENANCY.md §4.5, docs/DECISIONS.md 2026-08-15).
/// </summary>
public interface IAuthProvider
{
    /// <summary>Matches <c>OrganizationAuthConfiguration.Method</c>; one of
    /// <c>AuthMethodNames</c>. Providers are selected by this value.</summary>
    string Method { get; }

    Task<AuthResult> AuthenticateAsync(AuthRequest request, CancellationToken cancellationToken = default);
}
