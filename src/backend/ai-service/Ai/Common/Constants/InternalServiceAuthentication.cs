namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// The service-to-service handshake, named once because ai-service sits on both ends of it: its own
/// internal routes demand this header from their callers, and its learning-service client sends the same
/// header the other way.
///
/// <para>
/// <b>An unconfigured secret allows only in Development and refuses everywhere else</b> (40.34). The key
/// was configured in no compose file, so the earlier allow-when-unset behaviour made the check a no-op
/// exactly where it mattered.
/// </para>
/// </summary>
public static class InternalServiceAuthentication
{
    /// <summary>Header carrying the shared secret on every internal call.</summary>
    public const string HeaderName = "X-Internal-Service-Secret";

    /// <summary>Configuration path of the expected secret. A secret; injected from the environment.</summary>
    public const string SecretConfigurationKey = "InternalAuth:ServiceSecret";
}
