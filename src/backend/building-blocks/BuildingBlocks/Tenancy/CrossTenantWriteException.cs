namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Thrown by <see cref="TenantSaveChangesInterceptor"/> when a save would create, modify or delete an
/// <see cref="ITenantScoped"/> row belonging to an organization other than the scope's own.
///
/// <para>
/// This is a write-guard failure, not a validation failure: reaching it means application code
/// assembled a cross-tenant change, so it must surface as a 500-class fault the caller cannot retry
/// into success, never be caught and mapped to a 4xx that invites a retry with the same payload.
/// </para>
/// </summary>
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
