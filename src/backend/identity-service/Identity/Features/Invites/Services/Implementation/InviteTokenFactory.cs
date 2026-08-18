using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Invites.Services.Implementation;

/// <summary>
/// Token layout: <c>{organizationId:N}.{nonce}.{signature}</c>, all parts base64url, the signature
/// an HMAC-SHA256 over <c>{organizationId:N}.{nonce}</c>.
///
/// <para>
/// Carrying the organization inside the signed token is what lets the anonymous acceptance
/// endpoint scope its lookup: the value is verified before use and is not client-choosable, so it
/// belongs to the same trust class as a JWT claim rather than to the forbidden "organization read
/// from body/query/route" class (docs/TENANCY/TENANCY.md §1.3). The nonce supplies the entropy —
/// 32 bytes from a cryptographic RNG — so knowing the organization id gives an attacker nothing.
/// </para>
/// </summary>
internal sealed class InviteTokenFactory : IInviteTokenFactory
{
    private const int NonceByteCount = 32;
    private const char PartSeparator = '.';
    private const int TokenPartCount = 3;

    private readonly byte[] _signingKeyBytes;

    public InviteTokenFactory(
        IOptions<InviteConfiguration> inviteOptions,
        IOptions<JwtConfiguration> jwtOptions)
    {
        var configuredSigningKey = inviteOptions.Value.SigningKey;
        var effectiveSigningKey = string.IsNullOrWhiteSpace(configuredSigningKey)
            ? jwtOptions.Value.Key
            : configuredSigningKey;

        if (string.IsNullOrWhiteSpace(effectiveSigningKey))
        {
            throw new InvalidOperationException(
                "Neither Invites:SigningKey nor Jwt:Key is configured; invite tokens cannot be signed.");
        }

        _signingKeyBytes = Encoding.UTF8.GetBytes(effectiveSigningKey);
    }

    public IssuedInviteToken Issue(Guid organizationId)
    {
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(NonceByteCount));
        var signedPayload = $"{organizationId:N}{PartSeparator}{nonce}";
        var rawToken = $"{signedPayload}{PartSeparator}{ComputeSignature(signedPayload)}";

        return new IssuedInviteToken(rawToken, ComputeTokenHash(rawToken));
    }

    public bool TryReadOrganizationId(string rawToken, out Guid organizationId)
    {
        organizationId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var parts = rawToken.Split(PartSeparator);
        if (parts.Length != TokenPartCount)
        {
            return false;
        }

        var signedPayload = $"{parts[0]}{PartSeparator}{parts[1]}";
        var expectedSignature = ComputeSignature(signedPayload);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[2])))
        {
            return false;
        }

        return Guid.TryParseExact(parts[0], "N", out organizationId) && organizationId != Guid.Empty;
    }

    public string ComputeTokenHash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private string ComputeSignature(string signedPayload)
        => Base64UrlEncode(HMACSHA256.HashData(_signingKeyBytes, Encoding.UTF8.GetBytes(signedPayload)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
