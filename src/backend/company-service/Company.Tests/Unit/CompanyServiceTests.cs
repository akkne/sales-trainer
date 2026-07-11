using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Exceptions;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Infrastructure.Ai;
using Sellevate.Company.Tests.Helpers;

namespace Sellevate.Company.Tests.Unit;

[TestFixture]
public sealed class CompanyServiceTests
{
    private Infrastructure.Data.CompanyDbContext _databaseContext = null!;
    private string _databaseName = null!;
    private IBriefingAiClient _briefingAiClient = null!;
    private IParseLogAiClient _parseLogAiClient = null!;
    private IPersonaAiClient _personaAiClient = null!;
    private IReadinessAiClient _readinessAiClient = null!;
    private CompanyService _companyService = null!;

    private static readonly Guid FirstUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [SetUp]
    public void SetUp()
    {
        _databaseName = $"company-tests-{Guid.NewGuid()}";
        _databaseContext = TestCompanyDatabaseFactory.CreateInMemory(_databaseName);
        _briefingAiClient = Substitute.For<IBriefingAiClient>();
        _parseLogAiClient = Substitute.For<IParseLogAiClient>();
        _personaAiClient = Substitute.For<IPersonaAiClient>();
        _readinessAiClient = Substitute.For<IReadinessAiClient>();
        _companyService = new CompanyService(_databaseContext, _briefingAiClient, _parseLogAiClient, _personaAiClient, _readinessAiClient);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task CreateCompanyAsync_creates_company_owned_by_user()
    {
        var request = new CreateCompanyRequestDto("Acme Corp");

        var result = await _companyService.CreateCompanyAsync(FirstUserId, request);

        result.Name.Should().Be("Acme Corp");
        result.Description.Should().BeEmpty();
        result.Id.Should().NotBeEmpty();
    }

    [Test]
    public async Task CreateCompanyAsync_persists_description_when_provided()
    {
        var request = new CreateCompanyRequestDto("Acme Corp", "Manufacturer of anvils and rockets");

        var result = await _companyService.CreateCompanyAsync(FirstUserId, request);

        result.Description.Should().Be("Manufacturer of anvils and rockets");
    }

    [Test]
    public async Task CreateCompanyAsync_defaults_description_to_empty_when_omitted()
    {
        var request = new CreateCompanyRequestDto("Acme Corp");

        var result = await _companyService.CreateCompanyAsync(FirstUserId, request);

        result.Description.Should().BeEmpty();
    }

    [Test]
    public async Task GetCompanyAsync_returns_company_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetCompanyAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Company");
    }

    [Test]
    public async Task GetCompanyAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetCompanyAsync(SecondUserId, company.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetCompanyAsync_returns_null_for_nonexistent_company()
    {
        var result = await _companyService.GetCompanyAsync(FirstUserId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task ListCompaniesAsync_returns_only_user_companies()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "User One Company");
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, SecondUserId, "User Two Company");

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("User One Company");
    }

    [Test]
    public async Task ListCompaniesAsync_filters_by_search_term()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Acme Corporation");
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Beta Industries");

        var results = await _companyService.ListCompaniesAsync(FirstUserId, "acme");

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Acme Corporation");
    }

    [Test]
    public async Task ListCompaniesAsync_description_excerpt_is_truncated_to_160_chars()
    {
        var longDescription = new string('x', 300);
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Company", longDescription);

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Should().HaveCount(1);
        results[0].DescriptionExcerpt.Should().HaveLength(160);
    }

    [Test]
    public async Task UpdateCompanyAsync_updates_name_and_description()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Old Name");
        var request = new UpdateCompanyRequestDto("New Name", "New description");

        var result = await _companyService.UpdateCompanyAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Description.Should().Be("New description");
    }

    [Test]
    public async Task UpdateCompanyAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new UpdateCompanyRequestDto("New Name");

        var result = await _companyService.UpdateCompanyAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task CreateCompanyAsync_defaults_status_to_lead()
    {
        var request = new CreateCompanyRequestDto("Acme Corp");

        var result = await _companyService.CreateCompanyAsync(FirstUserId, request);

        result.Status.Should().Be(CompanyStatus.Lead);
    }

    [Test]
    public async Task UpdateCompanyStatusAsync_updates_status_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new UpdateCompanyStatusRequestDto(CompanyStatus.Contacted);

        var result = await _companyService.UpdateCompanyStatusAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Status.Should().Be(CompanyStatus.Contacted);
    }

    [Test]
    public async Task UpdateCompanyStatusAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new UpdateCompanyStatusRequestDto(CompanyStatus.DealWon);

        var result = await _companyService.UpdateCompanyStatusAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateCompanyStatusAsync_returns_null_for_nonexistent_company()
    {
        var request = new UpdateCompanyStatusRequestDto(CompanyStatus.DealLost);

        var result = await _companyService.UpdateCompanyStatusAsync(FirstUserId, Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateCompanyStatusAsync_persists_status_across_reads()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new UpdateCompanyStatusRequestDto(CompanyStatus.MeetingScheduled);

        await _companyService.UpdateCompanyStatusAsync(FirstUserId, company.Id, request);
        var result = await _companyService.GetCompanyAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(CompanyStatus.MeetingScheduled);
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_schedules_follow_up_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var nextActionAt = DateTime.UtcNow.AddDays(3);
        var request = new UpdateCompanyFollowUpRequestDto(nextActionAt, "Call about the proposal");

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.NextActionAt.Should().BeCloseTo(nextActionAt, TimeSpan.FromSeconds(1));
        result.NextActionNote.Should().Be("Call about the proposal");
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new UpdateCompanyFollowUpRequestDto(DateTime.UtcNow.AddDays(1), "note");

        var result = await _companyService.UpdateCompanyFollowUpAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_returns_null_for_nonexistent_company()
    {
        var request = new UpdateCompanyFollowUpRequestDto(DateTime.UtcNow.AddDays(1), "note");

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_rescheduling_resets_FollowUpNotifiedAt_so_it_notifies_again()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, FirstUserId, "Test Company",
            nextActionAt: DateTime.UtcNow.AddDays(-1),
            followUpNotifiedAt: DateTime.UtcNow.AddHours(-12));
        var newNextActionAt = DateTime.UtcNow.AddDays(5);
        var request = new UpdateCompanyFollowUpRequestDto(newNextActionAt, "follow up again");

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.FollowUpNotifiedAt.Should().BeNull();
        result.NextActionAt.Should().BeCloseTo(newNextActionAt, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_same_date_with_new_note_does_not_reset_FollowUpNotifiedAt()
    {
        var sameDate = DateTime.UtcNow.AddDays(3);
        var notifiedAt = DateTime.UtcNow.AddHours(-2);
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, FirstUserId, "Test Company",
            nextActionAt: sameDate,
            nextActionNote: "old note",
            followUpNotifiedAt: notifiedAt);
        var request = new UpdateCompanyFollowUpRequestDto(sameDate, "new note only");

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.NextActionNote.Should().Be("new note only");
        result.FollowUpNotifiedAt.Should().BeCloseTo(notifiedAt, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_different_date_resets_FollowUpNotifiedAt()
    {
        var originalDate = DateTime.UtcNow.AddDays(-1);
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, FirstUserId, "Test Company",
            nextActionAt: originalDate,
            followUpNotifiedAt: DateTime.UtcNow.AddHours(-1));
        var newDate = DateTime.UtcNow.AddDays(7);
        var request = new UpdateCompanyFollowUpRequestDto(newDate, null);

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.FollowUpNotifiedAt.Should().BeNull();
        result.NextActionAt.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_clearing_NextActionAt_clears_note_and_notified_at()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            _databaseContext, FirstUserId, "Test Company",
            nextActionAt: DateTime.UtcNow.AddDays(1),
            nextActionNote: "existing note",
            followUpNotifiedAt: DateTime.UtcNow.AddHours(-1));
        var request = new UpdateCompanyFollowUpRequestDto(null, null);

        var result = await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.NextActionAt.Should().BeNull();
        result.NextActionNote.Should().BeNull();
        result.FollowUpNotifiedAt.Should().BeNull();
    }

    [Test]
    public async Task UpdateCompanyFollowUpAsync_persists_across_reads()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var nextActionAt = DateTime.UtcNow.AddDays(2);
        var request = new UpdateCompanyFollowUpRequestDto(nextActionAt, "note");

        await _companyService.UpdateCompanyFollowUpAsync(FirstUserId, company.Id, request);
        var result = await _companyService.GetCompanyAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.NextActionAt.Should().BeCloseTo(nextActionAt, TimeSpan.FromSeconds(1));
        result.NextActionNote.Should().Be("note");
    }

    [Test]
    public async Task ListCompaniesAsync_includes_next_action_at_for_each_company()
    {
        var nextActionAt = DateTime.UtcNow.AddDays(1);
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Due Soon", nextActionAt: nextActionAt);
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "No Follow Up");

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Single(company => company.Name == "Due Soon").NextActionAt.Should().BeCloseTo(nextActionAt, TimeSpan.FromSeconds(1));
        results.Single(company => company.Name == "No Follow Up").NextActionAt.Should().BeNull();
    }

    [Test]
    public async Task ListCompaniesAsync_includes_status_for_each_company()
    {
        await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Won Deal", status: CompanyStatus.DealWon);

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Should().HaveCount(1);
        results[0].Status.Should().Be(CompanyStatus.DealWon);
    }

    [Test]
    public async Task DeleteCompanyAsync_removes_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var deleted = await _companyService.DeleteCompanyAsync(FirstUserId, company.Id);

        deleted.Should().BeTrue();
        var afterDelete = await _companyService.GetCompanyAsync(FirstUserId, company.Id);
        afterDelete.Should().BeNull();
    }

    [Test]
    public async Task DeleteCompanyAsync_returns_false_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var deleted = await _companyService.DeleteCompanyAsync(SecondUserId, company.Id);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_returns_null_for_nonexistent_company()
    {
        var request = new CreateCallLogEntryRequestDto("John", "Sales pitch", "Interested", DateTime.UtcNow);

        var result = await _companyService.CreateCallLogEntryAsync(FirstUserId, Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_returns_null_when_company_belongs_to_other_user()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCallLogEntryRequestDto("John", "Sales pitch", "Interested", DateTime.UtcNow);

        var result = await _companyService.CreateCallLogEntryAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_creates_entry_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCallLogEntryRequestDto("John Doe", "Q4 proposal", "Will review", DateTime.UtcNow);

        var result = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.ContactName.Should().Be("John Doe");
        result.CompanyId.Should().Be(company.Id);
    }

    [Test]
    public async Task CreateCallLogEntryAsync_accepts_empty_subject_and_outcome()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCallLogEntryRequestDto("Jane Doe", "", "", DateTime.UtcNow);

        var result = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Subject.Should().BeEmpty();
        result.Outcome.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateCallLogEntryAsync_accepts_empty_subject_and_outcome()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var createRequest = new CreateCallLogEntryRequestDto("John", "pitch", "ok", DateTime.UtcNow);
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, createRequest);

        var updateRequest = new UpdateCallLogEntryRequestDto("John", "", "", DateTime.UtcNow);
        var result = await _companyService.UpdateCallLogEntryAsync(FirstUserId, company.Id, entry!.Id, updateRequest);

        result.Should().NotBeNull();
        result!.Subject.Should().BeEmpty();
        result.Outcome.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateCallLogEntryAsync_returns_null_when_company_belongs_to_other_user()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var createRequest = new CreateCallLogEntryRequestDto("John", "pitch", "ok", DateTime.UtcNow);
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, createRequest);

        var updateRequest = new UpdateCallLogEntryRequestDto("Jane", "updated pitch", "updated ok", DateTime.UtcNow);
        var result = await _companyService.UpdateCallLogEntryAsync(SecondUserId, company.Id, entry!.Id, updateRequest);

        result.Should().BeNull();
    }

    [Test]
    public async Task DeleteCallLogEntryAsync_removes_entry()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var createRequest = new CreateCallLogEntryRequestDto("John", "pitch", "ok", DateTime.UtcNow);
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, createRequest);

        var deleted = await _companyService.DeleteCallLogEntryAsync(FirstUserId, company.Id, entry!.Id);

        deleted.Should().BeTrue();
        var entries = await _companyService.ListCallLogEntriesAsync(FirstUserId, company.Id);
        entries.Should().NotBeNull();
        entries!.Should().BeEmpty();
    }

    [Test]
    public async Task CreatePracticeCallAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreatePracticeCallRequestDto("session-123", "Close the deal");

        var result = await _companyService.CreatePracticeCallAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task CreatePracticeCallAsync_creates_entry_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreatePracticeCallRequestDto("session-abc", "Overcome price objection");

        var result = await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.DialogSessionId.Should().Be("session-abc");
        result.Goal.Should().Be("Overcome price objection");
    }

    [Test]
    public async Task GetRecentGoalsAsync_returns_last_5_distinct_goals_newest_first()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Excluded Goal", DateTime.UtcNow.AddDays(-10));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Goal E", DateTime.UtcNow.AddDays(-5));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Goal D", DateTime.UtcNow.AddDays(-4));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Goal C", DateTime.UtcNow.AddDays(-3));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Goal B", DateTime.UtcNow.AddDays(-2));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Newest Goal", DateTime.UtcNow.AddDays(-1));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Excluded Goal", DateTime.UtcNow.AddDays(-9));

        var result = await _companyService.GetRecentGoalsAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Should().HaveCount(5);
        result.Should().OnlyHaveUniqueItems();
        result[0].Should().Be("Newest Goal");
        result.Should().NotContain("Excluded Goal");
    }

    [Test]
    public async Task GetRecentGoalsAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "Some goal"));

        var result = await _companyService.GetRecentGoalsAsync(SecondUserId, company.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task ListCompaniesAsync_returns_counts_for_logs_and_practice_calls()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("John", "pitch", "ok", DateTime.UtcNow));
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Should().HaveCount(1);
        results[0].CallLogCount.Should().Be(1);
        results[0].PracticeCallCount.Should().Be(1);
    }

    [Test]
    public async Task CreateContactAsync_creates_contact_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCompanyContactRequestDto("Иван Петров", "Руководитель закупок", "Любит цифры");

        var result = await _companyService.CreateContactAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Иван Петров");
        result.Position.Should().Be("Руководитель закупок");
        result.Notes.Should().Be("Любит цифры");
        result.CompanyId.Should().Be(company.Id);
    }

    [Test]
    public async Task CreateContactAsync_defaults_position_and_notes_to_empty_when_omitted()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCompanyContactRequestDto("Иван Петров");

        var result = await _companyService.CreateContactAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Position.Should().BeEmpty();
        result.Notes.Should().BeEmpty();
    }

    [Test]
    public async Task CreateContactAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCompanyContactRequestDto("Иван Петров");

        var result = await _companyService.CreateContactAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task ListContactsAsync_returns_only_company_contacts_newest_first()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        await TestCompanyDatabaseFactory.SeedContactAsync(_databaseContext, FirstUserId, company.Id, "Older Contact");
        await TestCompanyDatabaseFactory.SeedContactAsync(_databaseContext, FirstUserId, otherCompany.Id, "Unrelated Contact");
        var newest = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Newest Contact"));

        var results = await _companyService.ListContactsAsync(FirstUserId, company.Id);

        results.Should().NotBeNull();
        results!.Select(contact => contact.Name).Should().Contain("Older Contact").And.Contain("Newest Contact");
        results.Should().NotContain(contact => contact.Name == "Unrelated Contact");
        results![0].Id.Should().Be(newest!.Id);
    }

    [Test]
    public async Task ListContactsAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var results = await _companyService.ListContactsAsync(SecondUserId, company.Id);

        results.Should().BeNull();
    }

    [Test]
    public async Task UpdateContactAsync_updates_name_position_and_notes()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Old Name"));
        var request = new UpdateCompanyContactRequestDto("New Name", "New Position", "New Notes");

        var result = await _companyService.UpdateContactAsync(FirstUserId, company.Id, contact!.Id, request);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New Name");
        result.Position.Should().Be("New Position");
        result.Notes.Should().Be("New Notes");
    }

    [Test]
    public async Task UpdateContactAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Name"));
        var request = new UpdateCompanyContactRequestDto("New Name");

        var result = await _companyService.UpdateContactAsync(SecondUserId, company.Id, contact!.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateContactAsync_defaults_position_and_notes_to_empty_when_omitted()
    {
        // DTO nullability alignment: CreateCompanyContactRequestDto and UpdateCompanyContactRequestDto
        // both treat Position/Notes as optional (nullable, default null), and the service normalizes
        // an omitted value to "" the same way for both create and update.
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(
            FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван Петров", "Руководитель закупок", "Любит цифры"));
        var request = new UpdateCompanyContactRequestDto("Иван Петров");

        var result = await _companyService.UpdateContactAsync(FirstUserId, company.Id, contact!.Id, request);

        result.Should().NotBeNull();
        result!.Position.Should().Be(string.Empty);
        result.Notes.Should().Be(string.Empty);
    }

    [Test]
    public async Task DeleteContactAsync_removes_contact()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Name"));

        var deleted = await _companyService.DeleteContactAsync(FirstUserId, company.Id, contact!.Id);

        deleted.Should().BeTrue();
        var contacts = await _companyService.ListContactsAsync(FirstUserId, company.Id);
        contacts.Should().NotBeNull();
        contacts!.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteContactAsync_returns_false_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Name"));

        var deleted = await _companyService.DeleteContactAsync(SecondUserId, company.Id, contact!.Id);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task ListCompaniesAsync_returns_contact_count()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Contact One"));
        await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Contact Two"));

        var results = await _companyService.ListCompaniesAsync(FirstUserId, null);

        results.Should().HaveCount(1);
        results[0].ContactCount.Should().Be(2);
    }

    [Test]
    public async Task GetCompanyAsync_returns_contact_count()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Contact One"));

        var result = await _companyService.GetCompanyAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.ContactCount.Should().Be(1);
    }

    [Test]
    public async Task CreateCallLogEntryAsync_links_contact_when_contactId_belongs_to_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван"));
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contact!.Id);

        var result = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.ContactId.Should().Be(contact.Id);
    }

    [Test]
    public async Task CreateCallLogEntryAsync_throws_ContactNotFoundInCompanyException_when_contactId_belongs_to_other_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        var contactFromOtherCompany = await _companyService.CreateContactAsync(FirstUserId, otherCompany.Id, new CreateCompanyContactRequestDto("Иван"));
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contactFromOtherCompany!.Id);

        Func<Task> act = () => _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        await act.Should().ThrowAsync<ContactNotFoundInCompanyException>();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_throws_ContactNotFoundInCompanyException_when_contactId_does_not_exist()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, Guid.NewGuid());

        Func<Task> act = () => _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        await act.Should().ThrowAsync<ContactNotFoundInCompanyException>();
    }

    [Test]
    public async Task UpdateCallLogEntryAsync_throws_ContactNotFoundInCompanyException_when_contactId_belongs_to_other_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        var contactFromOtherCompany = await _companyService.CreateContactAsync(FirstUserId, otherCompany.Id, new CreateCompanyContactRequestDto("Иван"));
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow));
        var updateRequest = new UpdateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contactFromOtherCompany!.Id);

        Func<Task> act = () => _companyService.UpdateCallLogEntryAsync(FirstUserId, company.Id, entry!.Id, updateRequest);

        await act.Should().ThrowAsync<ContactNotFoundInCompanyException>();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_translates_DbUpdateException_on_ContactId_fk_race_into_ContactNotFoundInCompanyException()
    {
        // Simulates the check-then-act race: EnsureContactBelongsToCompanyAsync's existence check
        // passes, but the contact is deleted by a concurrent request before SaveChangesAsync
        // commits, so Postgres would reject the insert with a ContactId FK violation. The
        // InMemory provider doesn't enforce FKs, so a SaveChangesInterceptor simulates that
        // violation deterministically for the entry being inserted here.
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван"));

        using var racingContext = TestCompanyDatabaseFactory.CreateInMemoryWithInterceptor(
            _databaseName, new ThrowOnCallLogContactIdInterceptor(contact!.Id));
        var racingService = new CompanyService(racingContext, _briefingAiClient, _parseLogAiClient, _personaAiClient, _readinessAiClient);
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contact.Id);

        Func<Task> act = () => racingService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        var exception = await act.Should().ThrowAsync<ContactNotFoundInCompanyException>();
        exception.Which.ContactId.Should().Be(contact.Id);
        exception.Which.InnerException.Should().BeOfType<DbUpdateException>();
    }

    [Test]
    public async Task UpdateCallLogEntryAsync_translates_DbUpdateException_on_ContactId_fk_race_into_ContactNotFoundInCompanyException()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван"));
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow));

        using var racingContext = TestCompanyDatabaseFactory.CreateInMemoryWithInterceptor(_databaseName,
            new ThrowOnCallLogContactIdInterceptor(contact!.Id));
        var racingService = new CompanyService(racingContext, _briefingAiClient, _parseLogAiClient, _personaAiClient, _readinessAiClient);
        var updateRequest = new UpdateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contact.Id);

        Func<Task> act = () => racingService.UpdateCallLogEntryAsync(FirstUserId, company.Id, entry!.Id, updateRequest);

        var exception = await act.Should().ThrowAsync<ContactNotFoundInCompanyException>();
        exception.Which.ContactId.Should().Be(contact.Id);
        exception.Which.InnerException.Should().BeOfType<DbUpdateException>();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_does_not_translate_an_unrelated_DbUpdateException()
    {
        // An unrelated DbUpdateException (different constraint, not the ContactId FK) must NOT be
        // mis-mapped to "contact not found" -> 400 — it has to keep propagating as-is (-> 500),
        // otherwise real database failures would be silently hidden from monitoring.
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван"));

        using var racingContext = TestCompanyDatabaseFactory.CreateInMemoryWithInterceptor(
            _databaseName,
            new ThrowOnCallLogContactIdInterceptor(contact!.Id, SimulatedDbUpdateFailure.UnrelatedConstraintViolation));
        var racingService = new CompanyService(racingContext, _briefingAiClient, _parseLogAiClient, _personaAiClient, _readinessAiClient);
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contact.Id);

        Func<Task> act = () => racingService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task DeleteContactAsync_leaves_call_log_entry_with_null_contactId_and_preserved_contactName()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var contact = await _companyService.CreateContactAsync(FirstUserId, company.Id, new CreateCompanyContactRequestDto("Иван"));
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contact!.Id));

        await _companyService.DeleteContactAsync(FirstUserId, company.Id, contact.Id);

        var logs = await _companyService.ListCallLogEntriesAsync(FirstUserId, company.Id);
        logs.Should().NotBeNull();
        var persistedEntry = logs!.Single(logEntry => logEntry.Id == entry!.Id);
        persistedEntry.ContactId.Should().BeNull();
        persistedEntry.ContactName.Should().Be("Иван");
    }

    [Test]
    public async Task GenerateBriefingAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GenerateBriefingAsync(SecondUserId, company.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task GenerateBriefingAsync_returns_null_for_nonexistent_company()
    {
        var result = await _companyService.GenerateBriefingAsync(FirstUserId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task GenerateBriefingAsync_caches_content_on_the_company_and_returns_it()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Продаёт виджеты");
        var generatedAt = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);
        _briefingAiClient
            .GenerateBriefingAsync(Arg.Any<BriefingAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BriefingAiResult("## Кто они\n- Продаёт виджеты", generatedAt));

        var result = await _companyService.GenerateBriefingAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Content.Should().Be("## Кто они\n- Продаёт виджеты");
        result.GeneratedAt.Should().Be(generatedAt);
    }

    [Test]
    public async Task GenerateBriefingAsync_passes_company_description_and_latest_goal_to_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Продаёт виджеты");
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Older goal", DateTime.UtcNow.AddDays(-2));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Newest goal", DateTime.UtcNow.AddDays(-1));
        _briefingAiClient
            .GenerateBriefingAsync(Arg.Any<BriefingAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BriefingAiResult("content", DateTime.UtcNow));

        await _companyService.GenerateBriefingAsync(FirstUserId, company.Id);

        await _briefingAiClient.Received(1).GenerateBriefingAsync(
            Arg.Is<BriefingAiRequest>(request =>
                request.CompanyDescription == "Продаёт виджеты" &&
                request.Goal == "Newest goal"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateBriefingAsync_passes_recent_call_logs_to_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("Иван", "Обсудили условия", "Взял паузу", DateTime.UtcNow));
        _briefingAiClient
            .GenerateBriefingAsync(Arg.Any<BriefingAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BriefingAiResult("content", DateTime.UtcNow));

        await _companyService.GenerateBriefingAsync(FirstUserId, company.Id);

        await _briefingAiClient.Received(1).GenerateBriefingAsync(
            Arg.Is<BriefingAiRequest>(request =>
                request.RecentCalls.Count == 1 &&
                request.RecentCalls[0].ContactName == "Иван" &&
                request.RecentCalls[0].Subject == "Обсудили условия"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateBriefingAsync_propagates_ai_failure_and_leaves_cache_unchanged()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Продаёт виджеты");
        _briefingAiClient
            .GenerateBriefingAsync(Arg.Any<BriefingAiRequest>(), Arg.Any<CancellationToken>())
            .Returns<BriefingAiResult>(_ => throw new InvalidOperationException("AI briefing service returned 503."));

        var act = () => _companyService.GenerateBriefingAsync(FirstUserId, company.Id);

        // The controller maps this exception to a 503 response (see CompanyController.GenerateBriefing).
        await act.Should().ThrowAsync<InvalidOperationException>();

        var cached = await _companyService.GetBriefingAsync(FirstUserId, company.Id);
        cached.Should().NotBeNull();
        cached!.Content.Should().BeNull();
        cached.GeneratedAt.Should().BeNull();
    }

    [Test]
    public async Task GetBriefingAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetBriefingAsync(SecondUserId, company.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBriefingAsync_returns_both_fields_null_when_never_generated()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetBriefingAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Content.Should().BeNull();
        result.GeneratedAt.Should().BeNull();
    }

    [Test]
    public async Task GetBriefingAsync_returns_cached_content_after_generation()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var generatedAt = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);
        _briefingAiClient
            .GenerateBriefingAsync(Arg.Any<BriefingAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BriefingAiResult("cached content", generatedAt));
        await _companyService.GenerateBriefingAsync(FirstUserId, company.Id);

        var result = await _companyService.GetBriefingAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Content.Should().Be("cached content");
        result.GeneratedAt.Should().Be(generatedAt);
    }

    [Test]
    public async Task ParseCallLogAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.ParseCallLogAsync(
            SecondUserId, company.Id, new ParseCallLogRequestDto("сырые заметки"));

        result.Should().BeNull();
    }

    [Test]
    public async Task ParseCallLogAsync_returns_null_for_nonexistent_company()
    {
        var result = await _companyService.ParseCallLogAsync(
            FirstUserId, Guid.NewGuid(), new ParseCallLogRequestDto("сырые заметки"));

        result.Should().BeNull();
    }

    [Test]
    public async Task ParseCallLogAsync_returns_parsed_fields_from_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        _parseLogAiClient
            .ParseLogAsync(Arg.Any<ParseLogAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParseLogAiResult("Иван Петров", "Обсудили условия", "Взял паузу подумать", new DateTime(2026, 7, 1)));

        var result = await _companyService.ParseCallLogAsync(
            FirstUserId, company.Id, new ParseCallLogRequestDto("сырые заметки"));

        result.Should().NotBeNull();
        result!.ContactName.Should().Be("Иван Петров");
        result.Subject.Should().Be("Обсудили условия");
        result.Outcome.Should().Be("Взял паузу подумать");
        result.OccurredAt.Should().Be(new DateTime(2026, 7, 1));
    }

    [Test]
    public async Task ParseCallLogAsync_passes_raw_text_to_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        _parseLogAiClient
            .ParseLogAsync(Arg.Any<ParseLogAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ParseLogAiResult(null, "subject", "outcome", null));

        await _companyService.ParseCallLogAsync(
            FirstUserId, company.Id, new ParseCallLogRequestDto("расшифровка звонка"));

        await _parseLogAiClient.Received(1).ParseLogAsync(
            Arg.Is<ParseLogAiRequest>(request => request.RawText == "расшифровка звонка"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ParseCallLogAsync_propagates_ai_failure()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        _parseLogAiClient
            .ParseLogAsync(Arg.Any<ParseLogAiRequest>(), Arg.Any<CancellationToken>())
            .Returns<ParseLogAiResult>(_ => throw new InvalidOperationException("AI parse-log service returned 503."));

        var act = () => _companyService.ParseCallLogAsync(
            FirstUserId, company.Id, new ParseCallLogRequestDto("сырые заметки"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task CreatePersonaAsync_creates_persona_for_correct_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCompanyPersonaRequestDto("Мария Соколова", "Руководитель закупок", "Прагматична и скептична.", PersonaDifficulty.Hard);

        var result = await _companyService.CreatePersonaAsync(FirstUserId, company.Id, request);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Мария Соколова");
        result.Position.Should().Be("Руководитель закупок");
        result.Personality.Should().Be("Прагматична и скептична.");
        result.Difficulty.Should().Be(PersonaDifficulty.Hard);
    }

    [Test]
    public async Task CreatePersonaAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCompanyPersonaRequestDto("Мария", "Закупщик", "Скептична.", PersonaDifficulty.Medium);

        var result = await _companyService.CreatePersonaAsync(SecondUserId, company.Id, request);

        result.Should().BeNull();
    }

    [Test]
    public async Task ListPersonasAsync_returns_only_company_personas_newest_first()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePersonaAsync(FirstUserId, company.Id, new CreateCompanyPersonaRequestDto("Older", "Pos", "Personality", PersonaDifficulty.Easy));
        var newest = await _companyService.CreatePersonaAsync(FirstUserId, company.Id, new CreateCompanyPersonaRequestDto("Newest", "Pos", "Personality", PersonaDifficulty.Easy));

        var results = await _companyService.ListPersonasAsync(FirstUserId, company.Id);

        results.Should().NotBeNull();
        results!.Should().HaveCount(2);
        results[0].Id.Should().Be(newest!.Id);
    }

    [Test]
    public async Task ListPersonasAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var results = await _companyService.ListPersonasAsync(SecondUserId, company.Id);

        results.Should().BeNull();
    }

    [Test]
    public async Task DeletePersonaAsync_removes_persona()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var persona = await _companyService.CreatePersonaAsync(FirstUserId, company.Id, new CreateCompanyPersonaRequestDto("Name", "Pos", "Personality", PersonaDifficulty.Medium));

        var deleted = await _companyService.DeletePersonaAsync(FirstUserId, company.Id, persona!.Id);

        deleted.Should().BeTrue();
        var personas = await _companyService.ListPersonasAsync(FirstUserId, company.Id);
        personas.Should().BeEmpty();
    }

    [Test]
    public async Task DeletePersonaAsync_returns_false_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var persona = await _companyService.CreatePersonaAsync(FirstUserId, company.Id, new CreateCompanyPersonaRequestDto("Name", "Pos", "Personality", PersonaDifficulty.Medium));

        var deleted = await _companyService.DeletePersonaAsync(SecondUserId, company.Id, persona!.Id);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task DeletePersonaAsync_returns_false_for_persona_from_another_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        var personaFromOtherCompany = await _companyService.CreatePersonaAsync(FirstUserId, otherCompany.Id, new CreateCompanyPersonaRequestDto("Name", "Pos", "Personality", PersonaDifficulty.Medium));

        var deleted = await _companyService.DeletePersonaAsync(FirstUserId, company.Id, personaFromOtherCompany!.Id);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task GeneratePersonaAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Описание");

        var result = await _companyService.GeneratePersonaAsync(
            SecondUserId, company.Id, new GenerateCompanyPersonaRequestDto(null, null, PersonaDifficulty.Medium));

        result.Should().BeNull();
    }

    [Test]
    public async Task GeneratePersonaAsync_returns_null_for_nonexistent_company()
    {
        var result = await _companyService.GeneratePersonaAsync(
            FirstUserId, Guid.NewGuid(), new GenerateCompanyPersonaRequestDto(null, null, PersonaDifficulty.Medium));

        result.Should().BeNull();
    }

    [Test]
    public async Task GeneratePersonaAsync_returns_fields_from_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Поставщик офисных принадлежностей");
        _personaAiClient
            .GeneratePersonaAsync(Arg.Any<PersonaAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PersonaAiResult("Мария Соколова", "Руководитель закупок", "Прагматична."));

        var result = await _companyService.GeneratePersonaAsync(
            FirstUserId, company.Id, new GenerateCompanyPersonaRequestDto("Иван", "Закупщик", PersonaDifficulty.Hard));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Мария Соколова");
        result.Position.Should().Be("Руководитель закупок");
        result.Personality.Should().Be("Прагматична.");
    }

    [Test]
    public async Task GeneratePersonaAsync_passes_company_description_and_seed_contact_to_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Поставщик офисных принадлежностей");
        _personaAiClient
            .GeneratePersonaAsync(Arg.Any<PersonaAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PersonaAiResult("Name", "Pos", "Personality"));

        await _companyService.GeneratePersonaAsync(
            FirstUserId, company.Id, new GenerateCompanyPersonaRequestDto("Иван", "Закупщик", PersonaDifficulty.Hard));

        await _personaAiClient.Received(1).GeneratePersonaAsync(
            Arg.Is<PersonaAiRequest>(request =>
                request.CompanyDescription == "Поставщик офисных принадлежностей" &&
                request.ContactName == "Иван" &&
                request.ContactPosition == "Закупщик" &&
                request.Difficulty == "Hard"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GeneratePersonaAsync_propagates_ai_failure()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company", "Описание");
        _personaAiClient
            .GeneratePersonaAsync(Arg.Any<PersonaAiRequest>(), Arg.Any<CancellationToken>())
            .Returns<PersonaAiResult>(_ => throw new InvalidOperationException("AI persona service returned 503."));

        var act = () => _companyService.GeneratePersonaAsync(
            FirstUserId, company.Id, new GenerateCompanyPersonaRequestDto(null, null, PersonaDifficulty.Medium));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GetReadinessAsync_returns_null_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetReadinessAsync(SecondUserId, company.Id);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetReadinessAsync_returns_null_for_nonexistent_company()
    {
        var result = await _companyService.GetReadinessAsync(FirstUserId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task GetReadinessAsync_returns_all_null_fields_when_no_practice_sessions()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");

        var result = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Score.Should().BeNull();
        result.Strengths.Should().BeNull();
        result.Gaps.Should().BeNull();
        result.Recommendation.Should().BeNull();
        result.GeneratedAt.Should().BeNull();
        await _readinessAiClient.DidNotReceive().GenerateReadinessAsync(
            Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetReadinessAsync_returns_all_null_fields_when_ai_service_signals_no_data()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns((ReadinessAiResult?)null);

        var result = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Score.Should().BeNull();
    }

    [Test]
    public async Task GetReadinessAsync_generates_and_caches_then_second_call_returns_cache_without_calling_ai_client_again()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "Close the deal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessAiResult(75, ["Уверенный тон"], ["Работа с ценой"], "Потренируйте возражения."));

        var firstResult = await _companyService.GetReadinessAsync(FirstUserId, company.Id);
        var secondResult = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        firstResult.Should().NotBeNull();
        firstResult!.Score.Should().Be(75);
        secondResult.Should().NotBeNull();
        secondResult!.Score.Should().Be(75);
        secondResult.Strengths.Should().ContainSingle().Which.Should().Be("Уверенный тон");
        secondResult.Recommendation.Should().Be("Потренируйте возражения.");
        await _readinessAiClient.Received(1).GenerateReadinessAsync(
            Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetReadinessAsync_passes_session_ids_and_latest_goal_to_ai_client()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Older goal", DateTime.UtcNow.AddDays(-2));
        await TestCompanyDatabaseFactory.SeedPracticeCallAsync(_databaseContext, FirstUserId, company.Id, "Newest goal", DateTime.UtcNow.AddDays(-1));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessAiResult(50, [], [], "Ок."));

        await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        await _readinessAiClient.Received(1).GenerateReadinessAsync(
            Arg.Is<ReadinessAiRequest>(request =>
                request.Goal == "Newest goal" &&
                request.SessionIds.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetReadinessAsync_propagates_ai_failure_and_leaves_cache_unchanged()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns<ReadinessAiResult?>(_ => throw new InvalidOperationException("AI readiness service returned 503."));

        var act = () => _companyService.GetReadinessAsync(FirstUserId, company.Id);

        // The controller maps this exception to a 503 response (see CompanyController.GetReadiness).
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Neither the positive cache nor the negative ("no feedback") cache should have been
        // written — a transport/AI failure must not be confused with a "no feedback yet" result.
        var persisted = await _databaseContext.Companies.FindAsync(company.Id);
        persisted!.ReadinessJson.Should().BeNull();
        persisted.ReadinessGeneratedAt.Should().BeNull();
        persisted.ReadinessNoFeedbackUntil.Should().BeNull();
    }

    [Test]
    public async Task GetReadinessAsync_negative_caches_no_feedback_result_so_repeat_call_does_not_refanout()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns((ReadinessAiResult?)null);

        var firstResult = await _companyService.GetReadinessAsync(FirstUserId, company.Id);
        var secondResult = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        firstResult.Should().NotBeNull();
        firstResult!.Score.Should().BeNull();
        secondResult.Should().NotBeNull();
        secondResult!.Score.Should().BeNull();

        // The second call must be served from the negative cache — it must NOT re-fan-out to
        // ai-service (which would itself re-run the sequential Mongo reads across sessions).
        await _readinessAiClient.Received(1).GenerateReadinessAsync(
            Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>());

        var persisted = await _databaseContext.Companies.FindAsync(company.Id);
        persisted!.ReadinessNoFeedbackUntil.Should().NotBeNull();
        persisted.ReadinessNoFeedbackUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Test]
    public async Task CreatePracticeCallAsync_invalidates_cached_readiness_so_next_get_regenerates_it()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessAiResult(40, [], [], "Первая рекомендация."));
        await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        // A new practice call is the completion signal that should invalidate the cache.
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-2", "goal 2"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessAiResult(90, [], [], "Вторая рекомендация."));

        var result = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Score.Should().Be(90);
        result.Recommendation.Should().Be("Вторая рекомендация.");
        await _readinessAiClient.Received(2).GenerateReadinessAsync(
            Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreatePracticeCallAsync_clears_negative_readiness_cache_so_next_get_refanouts()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "goal"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns((ReadinessAiResult?)null);
        // Negative-cache the "no feedback yet" result.
        await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        // A new practice call completing is the signal that feedback might now exist — it must
        // clear the negative cache too, not just the positive ReadinessJson cache.
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-2", "goal 2"));
        _readinessAiClient
            .GenerateReadinessAsync(Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessAiResult(60, [], [], "Готовность выросла."));

        var result = await _companyService.GetReadinessAsync(FirstUserId, company.Id);

        result.Should().NotBeNull();
        result!.Score.Should().Be(60);
        await _readinessAiClient.Received(2).GenerateReadinessAsync(
            Arg.Any<ReadinessAiRequest>(), Arg.Any<CancellationToken>());
    }
}

/// <summary>Which inner exception <see cref="ThrowOnCallLogContactIdInterceptor"/> simulates.</summary>
internal enum SimulatedDbUpdateFailure
{
    /// <summary>
    /// A real Postgres FK violation (SqlState 23503) against exactly
    /// <c>FK_CallLogEntries_CompanyContacts_ContactId</c> — the shape
    /// <c>CompanyService.IsContactIdForeignKeyViolation</c> is meant to catch and translate.
    /// </summary>
    ContactIdForeignKeyViolation,

    /// <summary>
    /// An unrelated failure (e.g. a different constraint, a transient error) that happens to also
    /// be a <see cref="DbUpdateException"/> with a <see cref="PostgresException"/> inner exception,
    /// but must NOT be translated into <see cref="ContactNotFoundInCompanyException"/> — it should
    /// keep propagating so it surfaces as a real 500 instead of a mis-mapped 400.
    /// </summary>
    UnrelatedConstraintViolation
}

/// <summary>
/// Test double for the ContactId FK-race scenario: fails <c>SaveChangesAsync</c> with a
/// <see cref="DbUpdateException"/> whenever the change set has an added/modified
/// <see cref="CallLogEntry"/> pointing at <paramref name="raceContactId"/>, standing in for the
/// FK violation a real Postgres database raises when that contact was concurrently deleted (the
/// InMemory provider used in these tests does not enforce foreign keys on its own). The inner
/// exception is a real <see cref="PostgresException"/> (constructed via its public constructor)
/// carrying the same <c>SqlState</c>/<c>ConstraintName</c> shape the production code inspects, so
/// the translation logic itself — not just this test double — is exercised.
/// </summary>
internal sealed class ThrowOnCallLogContactIdInterceptor(
    Guid raceContactId,
    SimulatedDbUpdateFailure failureMode = SimulatedDbUpdateFailure.ContactIdForeignKeyViolation) : SaveChangesInterceptor
{
    private const string ContactIdForeignKeyConstraintName = "FK_CallLogEntries_CompanyContacts_ContactId";

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var tripsRace = eventData.Context?.ChangeTracker.Entries<CallLogEntry>().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified &&
            entry.Entity.ContactId == raceContactId) ?? false;

        if (tripsRace)
        {
            var (sqlState, constraintName) = failureMode switch
            {
                SimulatedDbUpdateFailure.ContactIdForeignKeyViolation =>
                    (PostgresErrorCodes.ForeignKeyViolation, ContactIdForeignKeyConstraintName),
                SimulatedDbUpdateFailure.UnrelatedConstraintViolation =>
                    // 23505 = unique_violation on some unrelated constraint — same DbUpdateException/
                    // PostgresException shape, but not the ContactId FK, so it must not be translated.
                    (PostgresErrorCodes.UniqueViolation, "IX_CompanyContacts_SomeUnrelatedIndex"),
                _ => throw new ArgumentOutOfRangeException(nameof(failureMode))
            };

            var postgresException = new PostgresException(
                messageText: "Simulated database constraint violation for test purposes.",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: sqlState,
                detail: null,
                hint: null,
                position: 0,
                internalPosition: 0,
                internalQuery: null,
                where: null,
                schemaName: null,
                tableName: null,
                columnName: null,
                dataTypeName: null,
                constraintName: constraintName,
                file: null,
                line: null,
                routine: null);

            throw new DbUpdateException(
                "Simulated ContactId foreign key violation (contact deleted concurrently).",
                postgresException);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
