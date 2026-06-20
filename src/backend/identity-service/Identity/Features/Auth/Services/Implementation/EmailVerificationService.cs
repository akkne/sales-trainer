using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Email.Abstract;
using Sellevate.Identity.Infrastructure.Email.Models;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

internal sealed class EmailVerificationService(
    IdentityDbContext databaseContext,
    IEmailSender emailSender,
    IOptions<EmailVerificationConfiguration> emailVerificationOptions,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    public async Task GenerateAndSendCodeAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var configuration = emailVerificationOptions.Value;

        var existingCodes = await databaseContext.EmailVerificationCodes
            .Where(verificationCode => verificationCode.Email == normalizedEmail)
            .ToListAsync(cancellationToken);

        var mostRecentCode = existingCodes
            .OrderByDescending(verificationCode => verificationCode.CreatedAt)
            .FirstOrDefault();

        if (mostRecentCode is not null)
        {
            var secondsSinceLastCode = (DateTime.UtcNow - mostRecentCode.CreatedAt).TotalSeconds;
            if (secondsSinceLastCode < configuration.ResendCooldownSeconds)
            {
                var retryAfterSeconds = (int)Math.Ceiling(
                    configuration.ResendCooldownSeconds - secondsSinceLastCode);
                logger.LogWarning(
                    "Verification code requested during cooldown for {Email}; {RetryAfterSeconds}s remaining",
                    normalizedEmail,
                    retryAfterSeconds);
                throw new EmailVerificationCooldownException(retryAfterSeconds);
            }
        }

        databaseContext.EmailVerificationCodes.RemoveRange(existingCodes);

        var generatedCode = GenerateNumericCode(configuration.CodeLength);
        databaseContext.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            CodeHash = ComputeCodeHash(generatedCode),
            ExpiresAt = DateTime.UtcNow.AddMinutes(configuration.CodeLifetimeMinutes),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailAsync(
            BuildVerificationEmail(normalizedEmail, displayName, generatedCode, configuration.CodeLifetimeMinutes),
            cancellationToken);

        logger.LogInformation("Verification code generated and dispatched for {Email}", normalizedEmail);
    }

    public async Task<bool> VerifyCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var configuration = emailVerificationOptions.Value;

        var verificationCode = await databaseContext.EmailVerificationCodes
            .Where(storedCode => storedCode.Email == normalizedEmail)
            .OrderByDescending(storedCode => storedCode.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verificationCode is null)
        {
            logger.LogWarning("Verification attempted with no active code for {Email}", normalizedEmail);
            return false;
        }

        if (verificationCode.ExpiresAt < DateTime.UtcNow ||
            verificationCode.AttemptCount >= configuration.MaximumVerificationAttempts)
        {
            databaseContext.EmailVerificationCodes.Remove(verificationCode);
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Verification code expired or exhausted for {Email}", normalizedEmail);
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(verificationCode.CodeHash),
                Encoding.UTF8.GetBytes(ComputeCodeHash(code))))
        {
            verificationCode.AttemptCount++;
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Invalid verification code for {Email} (attempt {AttemptCount})",
                normalizedEmail,
                verificationCode.AttemptCount);
            return false;
        }

        databaseContext.EmailVerificationCodes.Remove(verificationCode);
        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Verification code accepted for {Email}", normalizedEmail);
        return true;
    }

    private static string GenerateNumericCode(int length)
    {
        var codeBuilder = new StringBuilder(length);
        for (var position = 0; position < length; position++)
            codeBuilder.Append(RandomNumberGenerator.GetInt32(0, 10));
        return codeBuilder.ToString();
    }

    private static string ComputeCodeHash(string code)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hashBytes);
    }

    private static EmailMessage BuildVerificationEmail(
        string email,
        string displayName,
        string code,
        int lifetimeMinutes)
    {
        var recipientName = string.IsNullOrWhiteSpace(displayName) ? email : displayName;
        var textBody =
            $"Hi {recipientName},\n\n" +
            $"Your Sellevate verification code is {code}.\n" +
            $"It expires in {lifetimeMinutes} minutes.\n\n" +
            "If you did not create an account, you can ignore this email.";
        var htmlBody =
            $"<p>Hi {recipientName},</p>" +
            $"<p>Your Sellevate verification code is <strong style=\"font-size:20px;letter-spacing:2px\">{code}</strong>.</p>" +
            $"<p>It expires in {lifetimeMinutes} minutes.</p>" +
            "<p>If you did not create an account, you can ignore this email.</p>";

        return new EmailMessage(
            RecipientEmail: email,
            RecipientName: recipientName,
            Subject: EmailVerificationConstants.EmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }
}
