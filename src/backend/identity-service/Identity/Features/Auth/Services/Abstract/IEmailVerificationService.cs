namespace Sellevate.Identity.Features.Auth.Services.Abstract;

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
