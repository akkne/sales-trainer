namespace Sellevate.Identity.Features.Auth.Exceptions;

public sealed class EmailVerificationCooldownException : Exception
{
    public EmailVerificationCooldownException(int retryAfterSeconds)
        : base("A verification code was requested too recently. Please wait before requesting another.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
