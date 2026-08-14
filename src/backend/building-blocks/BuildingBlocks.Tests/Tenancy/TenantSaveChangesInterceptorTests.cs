using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.BuildingBlocks.Tests.Tenancy.Support;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

[TestFixture]
public class TenantSaveChangesInterceptorTests
{
    private string _databaseName = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _databaseName = $"tenancy-tests-{Guid.NewGuid()}";
    }

    [Test]
    public async Task SaveChangesAsync_with_no_tenant_context_throws()
    {
        var tenantContext = new TenantContext();
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);
        await using var databaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, tenantContext, interceptor);
        databaseContext.TenantScopedTestEntities.Add(new TenantScopedTestEntity { Id = Guid.NewGuid(), Name = "Untenanted" });

        var act = () => databaseContext.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SaveChangesAsync_stamps_organization_id_when_unset()
    {
        var organizationId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);
        await using var databaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, tenantContext, interceptor);
        var entity = new TenantScopedTestEntity { Id = Guid.NewGuid(), Name = "Stamped" };
        databaseContext.TenantScopedTestEntities.Add(entity);

        await databaseContext.SaveChangesAsync();

        entity.OrganizationId.Should().Be(organizationId);
    }

    [Test]
    public async Task SaveChangesAsync_inserting_a_foreign_organization_id_throws_without_leaking_it()
    {
        var currentOrganizationId = Guid.NewGuid();
        var foreignOrganizationId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(currentOrganizationId);
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);
        await using var databaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, tenantContext, interceptor);
        databaseContext.TenantScopedTestEntities.Add(new TenantScopedTestEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = foreignOrganizationId,
            Name = "Foreign"
        });

        var act = () => databaseContext.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<CrossTenantWriteException>();
        thrown.Which.ExpectedOrganizationId.Should().Be(currentOrganizationId);
        thrown.Which.Message.Should().NotContain(foreignOrganizationId.ToString());
    }

    [Test]
    public async Task SaveChangesAsync_tampering_with_organization_id_on_an_entity_loaded_via_IgnoreQueryFilters_throws()
    {
        var ownerOrganizationId = Guid.NewGuid();
        var attackerOrganizationId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var seedTenantContext = new TenantContext();
        seedTenantContext.EnterSystemMode();
        await using (var seedDatabaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, seedTenantContext))
        {
            seedDatabaseContext.TenantScopedTestEntities.Add(new TenantScopedTestEntity
            {
                Id = entityId,
                OrganizationId = ownerOrganizationId,
                Name = "OwnedByAnotherOrganization"
            });
            await seedDatabaseContext.SaveChangesAsync();
        }

        var attackerTenantContext = new TenantContext();
        attackerTenantContext.SetOrganization(attackerOrganizationId);
        var interceptor = new TenantSaveChangesInterceptor(attackerTenantContext);
        await using var attackerDatabaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, attackerTenantContext, interceptor);

        var foreignEntity = await attackerDatabaseContext.TenantScopedTestEntities
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == entityId);
        foreignEntity.OrganizationId = attackerOrganizationId;

        var act = () => attackerDatabaseContext.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<CrossTenantWriteException>();
        thrown.Which.ExpectedOrganizationId.Should().Be(attackerOrganizationId);
        thrown.Which.Message.Should().NotContain(ownerOrganizationId.ToString());
    }

    [Test]
    public async Task SaveChangesAsync_in_system_mode_bypasses_the_organization_check()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();
        var interceptor = new TenantSaveChangesInterceptor(tenantContext);
        await using var databaseContext = TenantTestDatabaseFactory.CreateInMemory(_databaseName, tenantContext, interceptor);
        databaseContext.TenantScopedTestEntities.Add(new TenantScopedTestEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "SystemWrite"
        });

        var act = () => databaseContext.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }
}
