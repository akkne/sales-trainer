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
        entries.Should().BeEmpty();
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

        var goals = new[] { "Goal A", "Goal B", "Goal C", "Goal D", "Goal E", "Goal F", "Goal A" };
        foreach (var goal in goals)
        {
            await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
                new CreatePracticeCallRequestDto($"session-{Guid.NewGuid()}", goal));
        }

        var result = await _companyService.GetRecentGoalsAsync(FirstUserId, company.Id);

        result.Should().HaveCount(5);
        result.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public async Task GetRecentGoalsAsync_returns_empty_for_wrong_owner()
    {
        var company = await TestCompanyDatabaseFactory.SeedCompanyAsync(_databaseContext, FirstUserId, "Test Company");
        await _companyService.CreatePracticeCallAsync(FirstUserId, company.Id,
            new CreatePracticeCallRequestDto("session-1", "Some goal"));

        var result = await _companyService.GetRecentGoalsAsync(SecondUserId, company.Id);

        result.Should().BeEmpty();
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
}
