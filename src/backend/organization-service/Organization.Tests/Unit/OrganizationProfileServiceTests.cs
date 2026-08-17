using FluentAssertions;
using NUnit.Framework;
using NSubstitute;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Implementation;
using Sellevate.Organization.Infrastructure.Data;
using Sellevate.Organization.Tests.Helpers;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Uses the EF InMemory provider, so <c>Database.BeginTransactionAsync</c> in
/// <see cref="OrganizationProfileService.GetProfileAsync"/> is a documented no-op transaction —
/// these tests exercise the service's own logic (tenant resolution, JSON round-tripping,
/// create-vs-update branching), not the real Postgres RLS boundary, which is covered separately
/// by <c>TenantRowLevelSecurityIntegrationTests</c> in BuildingBlocks.Tests.
/// </summary>
[TestFixture]
public sealed class OrganizationProfileServiceTests
{
    private OrganizationDbContext _databaseContext = null!;
    private TenantContext _tenantContext = null!;
    private IEventPublisher _eventPublisher = null!;
    private OrganizationProfileService _organizationProfileService = null!;
    private string _databaseName = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = $"organization-profile-tests-{Guid.NewGuid()}";
        _tenantContext = new TenantContext();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _databaseContext = TestOrganizationDatabaseFactory.CreateInMemory(_tenantContext, _databaseName);
        _organizationProfileService = new OrganizationProfileService(_databaseContext, _tenantContext, _eventPublisher);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task GetProfileAsync_returns_null_when_the_organization_has_no_profile_yet()
    {
        _tenantContext.SetOrganization(Guid.NewGuid());

        var profile = await _organizationProfileService.GetProfileAsync();

        profile.Should().BeNull();
    }

    [Test]
    public void GetProfileAsync_throws_when_the_tenant_context_is_not_set()
    {
        var act = async () => await _organizationProfileService.GetProfileAsync();

        act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task UpsertProfileAsync_creates_a_profile_on_first_write()
    {
        var organizationId = Guid.NewGuid();
        _tenantContext.SetOrganization(organizationId);

        var request = new UpdateOrganizationProfileRequestDto(
            Product: "Sales training SaaS",
            Icp: "B2B sales teams, 20-200 reps",
            Objections: [new OrganizationObjectionDto("Too expensive", "high", "Show ROI in first month")],
            ScriptStages: ["Opener", "Discovery", "Close"],
            Tone: "consultative",
            Glossary: new Dictionary<string, string> { ["ICP"] = "Ideal Customer Profile" },
            BannedClaims: ["guaranteed to close every deal"]);

        var profile = await _organizationProfileService.UpsertProfileAsync(request);

        profile.Product.Should().Be("Sales training SaaS");
        profile.Objections.Should().ContainSingle(objection => objection.Text == "Too expensive");
        profile.ScriptStages.Should().Equal("Opener", "Discovery", "Close");
        profile.Tone.Should().Be("consultative");
        profile.Glossary.Should().ContainKey("ICP").WhoseValue.Should().Be("Ideal Customer Profile");
        profile.BannedClaims.Should().Equal("guaranteed to close every deal");
    }

    [Test]
    public async Task UpsertProfileAsync_updates_the_existing_row_instead_of_creating_a_second_one()
    {
        var organizationId = Guid.NewGuid();
        _tenantContext.SetOrganization(organizationId);

        await _organizationProfileService.UpsertProfileAsync(
            new UpdateOrganizationProfileRequestDto("First version", null, null, null, null, null, null));

        await using (var secondContext = TestOrganizationDatabaseFactory.CreateInMemory(_tenantContext, _databaseName))
        {
            var secondService = new OrganizationProfileService(secondContext, _tenantContext, _eventPublisher);
            await secondService.UpsertProfileAsync(
                new UpdateOrganizationProfileRequestDto("Second version", null, null, null, null, null, null));
        }

        await using var verifyContext = TestOrganizationDatabaseFactory.CreateInMemory(_tenantContext, _databaseName);
        verifyContext.OrganizationProfiles.Count().Should().Be(1);
        (await new OrganizationProfileService(verifyContext, _tenantContext, _eventPublisher).GetProfileAsync())!
            .Product.Should().Be("Second version");
    }

    [Test]
    public async Task GetProfileAsync_never_returns_another_organizations_profile()
    {
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();

        _tenantContext.SetOrganization(organizationAId);
        await _organizationProfileService.UpsertProfileAsync(
            new UpdateOrganizationProfileRequestDto("Organization A's product", null, null, null, null, null, null));

        var tenantContextB = new TenantContext();
        tenantContextB.SetOrganization(organizationBId);
        await using var contextForB = TestOrganizationDatabaseFactory.CreateInMemory(tenantContextB, _databaseName);
        var serviceForB = new OrganizationProfileService(contextForB, tenantContextB, _eventPublisher);

        var profileForB = await serviceForB.GetProfileAsync();

        profileForB.Should().BeNull();
    }
}
