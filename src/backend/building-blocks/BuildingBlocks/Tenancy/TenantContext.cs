namespace Sellevate.BuildingBlocks.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }

    public bool IsPlatformWide { get; private set; }

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

    /// <summary>
    /// Widens reads to every organization for a validated platform-staff principal. Deliberately
    /// compatible with <see cref="SetOrganization"/>: a Sellevate administrator who also belongs to
    /// an organization keeps that organization, because it is the one their writes land in — the
    /// widening applies to what they can see, not to where their changes go.
    ///
    /// <para>
    /// Not compatible with <see cref="EnterSystemMode"/> in either direction. A background job has
    /// no principal to be entitled by, so a scope that is both is a bug, not a broader privilege.
    /// </para>
    /// </summary>
    public void EnterPlatformMode()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("Tenant context is already in system mode for this scope.");
        }

        IsPlatformWide = true;
    }

    public void EnterSystemMode()
    {
        if (OrganizationId is not null)
        {
            throw new InvalidOperationException("Tenant context organization is already set for this scope.");
        }

        if (IsPlatformWide)
        {
            throw new InvalidOperationException("Tenant context is already in platform-wide mode for this scope.");
        }

        IsSystem = true;
    }
}
