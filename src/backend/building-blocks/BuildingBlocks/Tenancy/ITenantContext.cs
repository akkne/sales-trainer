namespace Sellevate.BuildingBlocks.Tenancy;

public interface ITenantContext
{
    Guid? OrganizationId { get; }

    bool IsSystem { get; }
}
