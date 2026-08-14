namespace Sellevate.BuildingBlocks.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }

    public bool IsSystem { get; private set; }

    public void SetOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id must not be empty.", nameof(organizationId));
        }

        if (IsSystem)
        {
            throw new InvalidOperationException("Tenant context is already in system mode for this scope.");
        }

        if (OrganizationId is { } alreadySetOrganizationId && alreadySetOrganizationId != organizationId)
        {
            throw new InvalidOperationException("Tenant context organization is already set for this scope.");
        }

        OrganizationId = organizationId;
    }

    public void EnterSystemMode()
    {
        if (OrganizationId is not null)
        {
            throw new InvalidOperationException("Tenant context organization is already set for this scope.");
        }

        IsSystem = true;
    }
}
