using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class EmailVerificationServiceTests
{
    private static EmailVerificationService Build(out RecordingEmailSender email, out Helpers.InMemoryHolder holder)
    {
        var db = InMemoryDbContextFactory.Create();
        email = new RecordingEmailSender();
        holder = new Helpers.InMemoryHolder(db);
        var options = Options.Create(new EmailVerificationConfiguration());
        return new EmailVerificationService(db, email, options, NullLogger<EmailVerificationService>.Instance);
    }

    [Test]
    public async Task GenerateAndSendCode_SendsEmail_AndStoresCode()
    {
        var service = Build(out var email, out var holder);

        await service.GenerateAndSendCodeAsync("user@test.com", "User");

        email.SentMessages.Should().ContainSingle();
        holder.Db.EmailVerificationCodes.Should().ContainSingle();
    }

    [Test]
    public async Task GenerateAndSendCode_Throws_OnResendWithinCooldown()
    {
        var service = Build(out _, out _);

        await service.GenerateAndSendCodeAsync("user@test.com", "User");
        var act = async () => await service.GenerateAndSendCodeAsync("user@test.com", "User");

        await act.Should().ThrowAsync<EmailVerificationCooldownException>();
    }

    [Test]
    public async Task VerifyCode_Succeeds_WithTheEmailedCode()
    {
        var service = Build(out var email, out _);
        await service.GenerateAndSendCodeAsync("user@test.com", "User");
        var code = TestCodeExtractor.ExtractSixDigitCode(email.SentMessages.Single().TextBody);

        var result = await service.VerifyCodeAsync("user@test.com", code);

        result.Should().BeTrue();
    }

    [Test]
    public async Task VerifyCode_Fails_WithWrongCode()
    {
        var service = Build(out _, out _);
        await service.GenerateAndSendCodeAsync("user@test.com", "User");

        var result = await service.VerifyCodeAsync("user@test.com", "000000");

        result.Should().BeFalse();
    }
}
