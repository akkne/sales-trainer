namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// What an <c>IAuthProvider</c> is handed once the login flow has decided which provider to
/// dispatch to (Phase 40.8 seam, docs/TENANCY/TENANCY.md §4.5).
///
/// <para>
/// It carries no organization on purpose. The organization has already done its job by the time
/// this record exists — it selected the provider — and a provider that could be handed an
/// organization would invite exactly the "read the tenant from the request" shape that
/// <c>scripts/tenancy-boundary-lint.py</c> forbids.
/// </para>
///
/// <para><see cref="Password"/> is nullable because a future non-password provider has none.</para>
/// </summary>
public sealed record AuthRequest(string Email, string? Password);
