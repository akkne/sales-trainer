namespace Sellevate.Identity.Features.Auth.Services.Abstract;

/// <summary>
/// Issues and checks the one-time email verification code. Generating a code invalidates any earlier
/// one for the same address, and a successful check consumes it. Requesting one inside the configured
/// cooldown throws <c>EmailVerificationCooldownException</c> rather than silently doing nothing, so the
/// caller can tell the user how long to wait.
/// </summary>
public interface IEmailVerificationService
{
    Task GenerateAndSendCodeAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);
}
