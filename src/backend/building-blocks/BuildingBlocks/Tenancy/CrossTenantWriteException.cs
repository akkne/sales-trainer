namespace Sellevate.BuildingBlocks.Tenancy;

public sealed class CrossTenantWriteException : Exception
{
    public string EntityName { get; }

    public Guid ExpectedOrganizationId { get; }

    public CrossTenantWriteException(string entityName, Guid expectedOrganizationId)
        : base($"Cross-tenant write blocked for '{entityName}': expected organization '{expectedOrganizationId}'.")
    {
        EntityName = entityName;
        ExpectedOrganizationId = expectedOrganizationId;
    }
}
