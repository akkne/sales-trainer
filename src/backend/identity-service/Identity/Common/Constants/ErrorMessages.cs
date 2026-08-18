namespace Sellevate.Identity.Common.Constants;

/// <summary>
/// Messages raised by identity-service outside a feature's own constants class. Feature-specific
/// wording stays with the feature (<c>Features/Invites/Constants</c>,
/// <c>Features/PlatformAdmin/Constants</c>, …).
/// </summary>
public static class ErrorMessages
{
    public const string OrganizationContextNotSet = "Organization context is not set.";

    public const string JwtSigningKeyTooShort =
        "Jwt:Key must be configured and at least 32 bytes (256 bits) long for HMAC-SHA256.";

    public const string AvatarStorageInitializationFailed =
        "Avatar storage initialization failed at startup; continuing without default avatars";
}
