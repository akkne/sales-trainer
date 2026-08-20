namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>The organization a demo request was provisioned into, named by exactly the three fields
/// a caller building a link or a confirmation screen needs.</summary>
public sealed record ProvisionedOrganizationDto(Guid Id, string Name, string Slug);
