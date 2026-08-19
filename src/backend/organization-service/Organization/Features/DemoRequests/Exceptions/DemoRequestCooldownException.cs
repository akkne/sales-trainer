namespace Sellevate.Organization.Features.DemoRequests.Exceptions;

/// <summary>
/// The same work email submitted a demo request too recently. Rendered as 429 with
/// <see cref="RetryAfterSeconds"/> in both the <c>Retry-After</c> header and the response body, mirroring
/// identity-service's <c>EmailVerificationCooldownException</c> shape so a client handles the two the
/// same way.
/// </summary>
public sealed class DemoRequestCooldownException : Exception
{
    public DemoRequestCooldownException(int retryAfterSeconds)
        : base("A demo request was submitted too recently from this address. Please wait before submitting another.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
