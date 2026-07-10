using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Company.Eventing;
using Sellevate.Company.Features.Companies.FollowUpReminders;
using Sellevate.Company.Tests.Helpers;

namespace Sellevate.Company.Tests.Unit;

[TestFixture]
public sealed class FollowUpReminderServiceTests
{
    private Infrastructure.Data.CompanyDbContext _databaseContext = null!;
    private IEventPublisher _eventPublisher = null!;
    private FollowUpReminderService _reminderService = null!;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestCompanyDatabaseFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IEventPublisher>();
        var options = Options.Create(new FollowUpReminderOptions { BatchSize = 100 });
        _reminderService = new FollowUpReminderService(
            _databaseContext, _eventPublisher, options, NullLogger<FollowUpReminderService>.Instance);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task ProcessDueFollowUpsAsync_publishes_for_a_due_and_unnotified_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, UserId, "Acme",
            nextActionAt: DateTime.UtcNow.AddMinutes(-5), nextActionNote: "Call about pricing");

        var publishedCount = await _reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(1);
        await _eventPublisher.Received(1).PublishAsync(
            Topics.CompanyFollowUpDue,
            company.UserId.ToString(),
            Topics.CompanyFollowUpDue,
            Arg.Is<CompanyFollowUpDueEvent>(payload =>
                payload.CompanyId == company.Id
                && payload.UserId == company.UserId
                && payload.CompanyName == "Acme"
                && payload.Note == "Call about pricing"),
            1,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_skips_a_future_follow_up()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, UserId, "Future Co", nextActionAt: DateTime.UtcNow.AddDays(1));

        var publishedCount = await _reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(0);
        await _eventPublisher.DidNotReceiveWithAnyArgs().PublishAsync<CompanyFollowUpDueEvent>(
            default!, default!, default!, default!, default, default);
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_skips_a_company_with_no_scheduled_follow_up()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, UserId, "No Follow Up");

        var publishedCount = await _reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(0);
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_once_only_guard_skips_an_already_notified_due_company()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, UserId, "Already Notified",
            nextActionAt: DateTime.UtcNow.AddDays(-2),
            followUpNotifiedAt: DateTime.UtcNow.AddDays(-1));

        var publishedCount = await _reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(0);
        await _eventPublisher.DidNotReceiveWithAnyArgs().PublishAsync<CompanyFollowUpDueEvent>(
            default!, default!, default!, default!, default, default);
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_claims_the_company_so_a_second_poll_does_not_republish()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, UserId, "Acme", nextActionAt: DateTime.UtcNow.AddMinutes(-5));

        var firstRunCount = await _reminderService.ProcessDueFollowUpsAsync();
        var secondRunCount = await _reminderService.ProcessDueFollowUpsAsync();

        firstRunCount.Should().Be(1);
        secondRunCount.Should().Be(0);
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CompanyFollowUpDueEvent>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_sets_FollowUpNotifiedAt_on_the_claimed_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, UserId, "Acme", nextActionAt: DateTime.UtcNow.AddMinutes(-5));

        await _reminderService.ProcessDueFollowUpsAsync();

        var persisted = await _databaseContext.Companies.FindAsync(company.Id);
        persisted!.FollowUpNotifiedAt.Should().NotBeNull();
    }

    [Test]
    public async Task ProcessDueFollowUpsAsync_processes_multiple_due_companies_in_one_tick()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, UserId, "Co A", nextActionAt: DateTime.UtcNow.AddMinutes(-10));
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, UserId, "Co B", nextActionAt: DateTime.UtcNow.AddMinutes(-5));
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, UserId, "Co C (future)", nextActionAt: DateTime.UtcNow.AddDays(1));

        var publishedCount = await _reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(2);
    }
}
