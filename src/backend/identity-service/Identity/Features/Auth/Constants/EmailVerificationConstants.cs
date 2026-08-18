namespace Sellevate.Identity.Features.Auth.Constants;

/// <summary>
/// Wording used by the email verification flow. <see cref="InvalidCodeMessage"/> is deliberately the
/// same answer for "no code was ever issued", "the code expired", "too many wrong guesses" and "wrong
/// code": distinguishing them would tell a caller whether an address is known and how close they are.
/// </summary>
public static class EmailVerificationConstants
{
    public const string EmailSubject = "Your Sellevate verification code";
    public const string InvalidCodeMessage = "Invalid or expired verification code.";
}
