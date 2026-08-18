using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Ai.Common.Constants;

namespace Sellevate.Ai.Common.Extensions;

/// <summary>
/// Wires JWT bearer authentication, the four platform authorization policies, and the browser CORS
/// policy.
///
/// <para>
/// The signing key is validated at startup and the host refuses to start without a usable one. That is
/// deliberate: HMAC-SHA256 silently accepts a short key, so a deployment that forgot to inject the secret
/// would come up minting tokens anybody could forge. Failing to boot is the only failure mode of this
/// mistake that somebody notices.
/// </para>
/// </summary>
public static class AiAuthenticationServiceCollectionExtensions
{
    /// <summary>Shortest key HMAC-SHA256 may be trusted with: 256 bits, the digest width.</summary>
    private const int MinimumJwtSigningKeyByteCount = 32;

    /// <summary>Origin allowed through CORS when none is configured — the local development frontend.</summary>
    private const string DefaultFrontendOrigin = "http://localhost:3000";

    public static IServiceCollection AddAiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSigningKey = configuration[AiConfigurationKeys.JwtSigningKey];
        if (string.IsNullOrEmpty(jwtSigningKey)
            || Encoding.UTF8.GetByteCount(jwtSigningKey) < MinimumJwtSigningKeyByteCount)
        {
            throw new InvalidOperationException(
                $"{AiConfigurationKeys.JwtSigningKey} must be configured and at least "
                + $"{MinimumJwtSigningKeyByteCount} bytes (256 bits) long for HMAC-SHA256.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration[AiConfigurationKeys.JwtIssuer],
                    ValidateAudience = true,
                    ValidAudience = configuration[AiConfigurationKeys.JwtAudience],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
                };
            });

        services.AddAuthorization(AuthorizationPolicies.Register);

        var allowedOrigins = (configuration[AiConfigurationKeys.FrontendUrl] ?? DefaultFrontendOrigin)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
            corsPolicy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }
}
