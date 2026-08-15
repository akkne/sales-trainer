using Microsoft.Extensions.DependencyInjection;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>
/// Seeds rows into identity-service's projection of the tenant registry. In production the rows
/// arrive over Kafka from organization-service; the integration tests run without a broker, so
/// they write what the consumer would have written.
/// </summary>
public static class TestOrganizationSeeder
{
    public static async Task<OrganizationReplica> SeedOrganizationAsync(
        TestWebApplicationFactory factory,
        Guid organizationId,
        string name = "Test Organization",
        OrganizationReplicaStatus status = OrganizationReplicaStatus.Active)
    {
        var replica = new OrganizationReplica
        {
            OrganizationId = organizationId,
            Name = name,
            Slug = $"organization-{organizationId:N}",
            Status = status,
            UpdatedAt = DateTime.UtcNow,
        };

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        database.OrganizationReplicas.Add(replica);
        await database.SaveChangesAsync();
        return replica;
    }

    public static async Task SetStatusAsync(
        TestWebApplicationFactory factory,
        Guid organizationId,
        OrganizationReplicaStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var replica = await database.OrganizationReplicas.FindAsync(organizationId)
            ?? throw new InvalidOperationException($"No organization replica for {organizationId}.");
        replica.Status = status;
        replica.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync();
    }
}
