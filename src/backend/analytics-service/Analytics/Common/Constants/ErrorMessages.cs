namespace Sellevate.Analytics.Common.Constants;

public static class ErrorMessages
{
    public const string UnknownEventOrPage = "Unknown event or page.";
    public const string MissingOrMalformedBody = "Request body is missing or malformed.";
    public const string MissingUserIdentity = "The request is missing a resolved user identity.";
    public const string JwtSigningKeyTooShort =
        "Jwt:Key must be configured and at least 32 bytes (256 bits) long for HMAC-SHA256.";
}
