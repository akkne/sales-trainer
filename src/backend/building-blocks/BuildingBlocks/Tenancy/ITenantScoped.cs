namespace Sellevate.BuildingBlocks.Tenancy;

public interface ITenantScoped
{
    Guid OrganizationId { get; set; }
}
