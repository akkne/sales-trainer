using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Implementation;
using Sellevate.Organization.Infrastructure.Configuration;
using Sellevate.Organization.Infrastructure.Data;
using Sellevate.Organization.Tests.Helpers;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class DemoRequestServiceTests
{
    private const string ConfiguredNotificationEmail = "sales@sellevate.example";

    private OrganizationDbContext _databaseContext = null!;
    private IEmailSender _emailSender = null!;
    private DemoRequestService _demoRequestService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestOrganizationDatabaseFactory.CreateInMemory();
        _emailSender = Substitute.For<IEmailSender>();
        _demoRequestService = BuildService(new DemoRequestConfiguration
        {
            NotificationEmail = ConfiguredNotificationEmail,
            SubmissionCooldownSeconds = 300,
        });
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private DemoRequestService BuildService(DemoRequestConfiguration configuration) => new(
        _databaseContext,
        _emailSender,
        new DemoRequestNotificationComposer(Options.Create(new FrontendConfiguration())),
        Options.Create(configuration),
        NullLogger<DemoRequestService>.Instance);

    private static CreateDemoRequestRequestDto SampleRequest(
        string? workEmail = null, string? website = null, bool marketingConsentGiven = false) => new(
        FullName: "Jane Doe",
        WorkEmail: workEmail ?? "jane@example.com",
        Phone: "+7 900 123-45-67",
        CompanyName: "Acme Inc",
        JobTitle: null,
        SalesTeamSize: SalesTeamSize.SixToTwenty,
        Comment: "Interested in a pilot.",
        ConsentGiven: true,
        MarketingConsentGiven: marketingConsentGiven,
        Website: website);

    [Test]
    public async Task SubmitAsync_persists_the_lead_with_a_normalized_email()
    {
        var result = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "  Jane@Example.COM  "));

        var stored = await _databaseContext.DemoRequests.FindAsync(result.Id);
        stored.Should().NotBeNull();
        stored!.WorkEmail.Should().Be("jane@example.com");
    }

    [Test]
    public async Task SubmitAsync_sends_exactly_one_email_to_the_configured_inbox()
    {
        await _demoRequestService.SubmitAsync(SampleRequest());

        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(message => message.RecipientEmail == ConfiguredNotificationEmail),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_sends_only_the_acknowledgement_when_notification_email_is_blank()
    {
        var service = BuildService(new DemoRequestConfiguration { NotificationEmail = string.Empty });

        await service.SubmitAsync(SampleRequest());

        await _emailSender.Received(1).SendEmailAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(message => message.RecipientEmail == "jane@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_sends_a_submission_acknowledgement_to_the_submitters_work_email()
    {
        await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "acknowledged@example.com"));

        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(message => message.RecipientEmail == "acknowledged@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_still_persists_the_lead_and_still_sends_the_internal_notification_when_the_acknowledgement_send_throws()
    {
        _emailSender.SendEmailAsync(
                Arg.Is<EmailMessage>(message => message.RecipientEmail == "jane@example.com"),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("The submitter's mailbox rejected the acknowledgement."));

        var result = await _demoRequestService.SubmitAsync(SampleRequest());

        (await _databaseContext.DemoRequests.FindAsync(result.Id)).Should().NotBeNull();
        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(message => message.RecipientEmail == ConfiguredNotificationEmail),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SubmitAsync_still_succeeds_when_the_sender_throws()
    {
        _emailSender.SendEmailAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("MailerSend is down."));

        var result = await _demoRequestService.SubmitAsync(SampleRequest());

        result.Id.Should().NotBeEmpty();
        (await _databaseContext.DemoRequests.FindAsync(result.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task SubmitAsync_honeypot_submission_persists_nothing_and_sends_nothing_but_returns_an_id()
    {
        var result = await _demoRequestService.SubmitAsync(SampleRequest(website: "https://spam.example"));

        result.Id.Should().NotBeEmpty();
        (await _databaseContext.DemoRequests.AnyAsync()).Should().BeFalse();
        await _emailSender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, cancellationToken: default);
    }

    [Test]
    public async Task SubmitAsync_throws_DemoRequestCooldownException_for_a_second_submission_within_the_cooldown()
    {
        await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "repeat@example.com"));

        var act = async () => await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "repeat@example.com"));

        var thrown = await act.Should().ThrowAsync<DemoRequestCooldownException>();
        thrown.Which.RetryAfterSeconds.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(300);
    }

    [Test]
    public async Task SubmitAsync_succeeds_for_a_submission_after_the_cooldown_has_elapsed()
    {
        var initial = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "later@example.com"));
        var storedInitial = await _databaseContext.DemoRequests.FindAsync(initial.Id);
        storedInitial!.CreatedAt = DateTime.UtcNow.AddSeconds(-301);
        await _databaseContext.SaveChangesAsync();

        var act = async () => await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "later@example.com"));

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task SubmitAsync_stamps_MarketingConsentGivenAt_when_marketing_consent_is_given()
    {
        var result = await _demoRequestService.SubmitAsync(SampleRequest(marketingConsentGiven: true));

        var stored = await _databaseContext.DemoRequests.FindAsync(result.Id);
        stored!.MarketingConsentGivenAt.Should().NotBeNull();
        stored.MarketingConsentGivenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task SubmitAsync_leaves_MarketingConsentGivenAt_null_when_marketing_consent_is_not_given()
    {
        var result = await _demoRequestService.SubmitAsync(SampleRequest(marketingConsentGiven: false));

        var stored = await _databaseContext.DemoRequests.FindAsync(result.Id);
        stored!.MarketingConsentGivenAt.Should().BeNull();
    }

    [Test]
    public async Task SubmitAsync_stamps_the_required_ConsentGivenAt_regardless_of_the_marketing_answer()
    {
        var withMarketing = await _demoRequestService.SubmitAsync(
            SampleRequest(workEmail: "with-marketing@example.com", marketingConsentGiven: true));
        var withoutMarketing = await _demoRequestService.SubmitAsync(
            SampleRequest(workEmail: "without-marketing@example.com", marketingConsentGiven: false));

        var storedWithMarketing = await _databaseContext.DemoRequests.FindAsync(withMarketing.Id);
        var storedWithoutMarketing = await _databaseContext.DemoRequests.FindAsync(withoutMarketing.Id);

        storedWithMarketing!.ConsentGivenAt.Should().NotBe(default(DateTime));
        storedWithoutMarketing!.ConsentGivenAt.Should().NotBe(default(DateTime));
    }

    [Test]
    public async Task SubmitAsync_notification_email_states_marketing_consent_was_given()
    {
        EmailMessage? capturedMessage = null;
        _emailSender
            .When(sender => sender.SendEmailAsync(
                Arg.Is<EmailMessage>(message => message.RecipientEmail == ConfiguredNotificationEmail),
                Arg.Any<CancellationToken>()))
            .Do(callInfo => capturedMessage = callInfo.Arg<EmailMessage>());

        await _demoRequestService.SubmitAsync(SampleRequest(marketingConsentGiven: true));

        capturedMessage.Should().NotBeNull();
        capturedMessage!.TextBody.Should().Contain("Marketing consent: Yes");
        capturedMessage.HtmlBody.Should().Contain("Marketing consent:</strong> Yes");
    }

    [Test]
    public async Task SubmitAsync_notification_email_states_marketing_consent_was_declined()
    {
        EmailMessage? capturedMessage = null;
        _emailSender
            .When(sender => sender.SendEmailAsync(
                Arg.Is<EmailMessage>(message => message.RecipientEmail == ConfiguredNotificationEmail),
                Arg.Any<CancellationToken>()))
            .Do(callInfo => capturedMessage = callInfo.Arg<EmailMessage>());

        await _demoRequestService.SubmitAsync(SampleRequest(marketingConsentGiven: false));

        capturedMessage.Should().NotBeNull();
        capturedMessage!.TextBody.Should().Contain("Marketing consent: No");
        capturedMessage.HtmlBody.Should().Contain("Marketing consent:</strong> No");
    }

    [Test]
    public async Task UpdateStatusAsync_sends_the_approval_email_on_an_actual_transition_into_Approved()
    {
        var submitted = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "approved@example.com"));
        _emailSender.ClearReceivedCalls();

        await _demoRequestService.UpdateStatusAsync(submitted.Id, DemoRequestStatus.Approved);

        await _emailSender.Received(1).SendEmailAsync(
            Arg.Is<EmailMessage>(message => message.RecipientEmail == "approved@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateStatusAsync_sends_nothing_when_an_already_approved_lead_is_repatched_to_Approved()
    {
        var submitted = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "repatched@example.com"));
        await _demoRequestService.UpdateStatusAsync(submitted.Id, DemoRequestStatus.Approved);
        _emailSender.ClearReceivedCalls();

        await _demoRequestService.UpdateStatusAsync(submitted.Id, DemoRequestStatus.Approved);

        await _emailSender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, cancellationToken: default);
    }

    [Test]
    public async Task UpdateStatusAsync_sends_nothing_for_a_transition_from_New_to_Declined()
    {
        var submitted = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "declined@example.com"));
        _emailSender.ClearReceivedCalls();

        await _demoRequestService.UpdateStatusAsync(submitted.Id, DemoRequestStatus.Declined);

        await _emailSender.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, cancellationToken: default);
    }

    [Test]
    public async Task UpdateStatusAsync_still_records_the_approval_when_the_approval_email_throws()
    {
        var submitted = await _demoRequestService.SubmitAsync(SampleRequest(workEmail: "approval-fails@example.com"));
        _emailSender.SendEmailAsync(
                Arg.Is<EmailMessage>(message => message.RecipientEmail == "approval-fails@example.com"),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("MailerSend is down."));

        var result = await _demoRequestService.UpdateStatusAsync(submitted.Id, DemoRequestStatus.Approved);

        result.Should().NotBeNull();
        result!.Status.Should().Be(nameof(DemoRequestStatus.Approved));
        var stored = await _databaseContext.DemoRequests.FindAsync(submitted.Id);
        stored!.Status.Should().Be(DemoRequestStatus.Approved);
    }
}
