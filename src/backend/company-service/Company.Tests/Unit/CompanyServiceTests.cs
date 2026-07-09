using FluentAssertions;
using NUnit.Framework;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Tests.Helpers;

namespace Sellevate.Company.Tests.Unit;

[TestFixture]
public sealed class CompanyServiceTests
{
    private Infrastructure.Data.CompanyDbContext _databaseContext = null!;
    private CompanyService _companyService = null!;

    private static readonly Guid FirstUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestCompanyDatabaseFactory.CreateInMemory();
        _companyService = new CompanyService(_databaseContext);
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
    public async Task CreateCallLogEntryAsync_throws_when_contactId_belongs_to_other_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        var contactFromOtherCompany = await _companyService.CreateContactAsync(FirstUserId, otherCompany.Id, new CreateCompanyContactRequestDto("Иван"));
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contactFromOtherCompany!.Id);

        Func<Task> act = () => _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task CreateCallLogEntryAsync_throws_when_contactId_does_not_exist()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var request = new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, Guid.NewGuid());

        Func<Task> act = () => _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateCallLogEntryAsync_throws_when_contactId_belongs_to_other_company()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        var otherCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Other Company");
        var contactFromOtherCompany = await _companyService.CreateContactAsync(FirstUserId, otherCompany.Id, new CreateCompanyContactRequestDto("Иван"));
        var entry = await _companyService.CreateCallLogEntryAsync(FirstUserId, company.Id,
            new CreateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow));
        var updateRequest = new UpdateCallLogEntryRequestDto("Иван", "pitch", "ok", DateTime.UtcNow, contactFromOtherCompany!.Id);

        Func<Task> act = () => _companyService.UpdateCallLogEntryAsync(FirstUserId, company.Id, entry!.Id, updateRequest);

        await act.Should().ThrowAsync<InvalidOperationException>();
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
}
