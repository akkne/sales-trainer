using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

/// <summary>
/// Verifies a Google identity token against Google's published keys and this deployment's OAuth client
/// id. The audience check is the point: a token minted for a different application is cryptographically
/// valid and must still be refused. Nothing here decides whether the identity may sign in — that is
/// <see cref="Sellevate.Identity.Features.Auth.Services.Abstract.IAuthenticationService"/>'s call.
/// </summary>
internal sealed class GoogleTokenValidator(IOptions<GoogleAuthConfiguration> googleOptions) : IGoogleTokenValidator
{
    public async Task<GoogleUserPayload> ValidateAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default)
    {
        var googleClientId = googleOptions.Value.ClientId
            ?? throw new InvalidOperationException("Google:ClientId not configured.");

        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [googleClientId]
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(googleIdToken, validationSettings);

        return new GoogleUserPayload(
            Subject: payload.Subject,
            Email: payload.Email,
            Name: payload.Name,
            IsEmailVerified: payload.EmailVerified);
    }
}
