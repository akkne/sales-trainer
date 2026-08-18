namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// The handful of fields the sign-in flow uses out of a validated Google ID token, decoupled from
/// the Google SDK type so the flow can be exercised without a live token.
/// </summary>
public sealed record GoogleUserPayload(
    string Subject,
    string Email,
    string? Name,
    bool IsEmailVerified);
