using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Exceptions;
using SalesTrainer.Api.Features.Auth.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class EmailVerificationServiceTests
{
    private AppDbContext _db = null!;
    private RecordingEmailSender _emailSender = null!;

    private EmailVerificationService CreateService(EmailVerificationConfiguration configuration) =>
        new(_db, _emailSender, Options.Create(configuration), NullLogger<EmailVerificationService>.Instance);

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();
        _emailSender = new RecordingEmailSender();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GenerateAndSend_StoresSingleActiveCodeAndEmailsIt()
    {
        var service = CreateService(new EmailVerificationConfiguration());

        await service.GenerateAndSendCodeAsync("user@example.com", "User");

        _db.EmailVerificationCodes.Count(code => code.Email == "user@example.com").Should().Be(1);
        _emailSender.GetLastCodeFor("user@example.com").Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task VerifyCode_CorrectCode_ReturnsTrueAndConsumesCode()
    {
        var service = CreateService(new EmailVerificationConfiguration());
        await service.GenerateAndSendCodeAsync("consume@example.com", "User");
        var code = _emailSender.GetLastCodeFor("consume@example.com")!;

        var isValid = await service.VerifyCodeAsync("consume@example.com", code);

        isValid.Should().BeTrue();
        _db.EmailVerificationCodes.Any(stored => stored.Email == "consume@example.com").Should().BeFalse();
    }

    [Test]
    public async Task VerifyCode_ExceedingMaxAttempts_InvalidatesCode()
    {
        var service = CreateService(new EmailVerificationConfiguration { MaximumVerificationAttempts = 2 });
        await service.GenerateAndSendCodeAsync("lockout@example.com", "User");

        (await service.VerifyCodeAsync("lockout@example.com", "111111")).Should().BeFalse();
        (await service.VerifyCodeAsync("lockout@example.com", "222222")).Should().BeFalse();
        (await service.VerifyCodeAsync("lockout@example.com", "333333")).Should().BeFalse();

        _db.EmailVerificationCodes.Any(stored => stored.Email == "lockout@example.com").Should().BeFalse();
    }

    [Test]
    public async Task GenerateAndSend_WithinCooldown_ThrowsCooldownException()
    {
        var service = CreateService(new EmailVerificationConfiguration { ResendCooldownSeconds = 60 });
        await service.GenerateAndSendCodeAsync("cooldown@example.com", "User");

        var act = async () => await service.GenerateAndSendCodeAsync("cooldown@example.com", "User");

        await act.Should().ThrowAsync<EmailVerificationCooldownException>();
    }
}
