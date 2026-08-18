using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

/// <summary>
/// Wraps Google's static signature validation so the sign-in rules around it — invite-only access
/// and the active-membership requirement (40.7) — are reachable from a test without a live Google
/// token.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleUserPayload> ValidateAsync(string googleIdToken, CancellationToken cancellationToken = default);
}
