namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// Every field is optional, because the whole point of provisioning from a lead is that the lead
/// already carries everything needed: <c>organizationName</c> defaults to
/// <see cref="DemoRequest.CompanyName"/>, <c>slug</c> to a normalized form of that name,
/// <c>adminEmail</c> to <see cref="DemoRequest.WorkEmail"/>, and <c>role</c> to
/// <c>TenancySuperAdmin</c>. The fields exist for the one case where the default is wrong — a lead
/// spelled the company name unusually, or wants the invite to land on a different address than the one
/// the form was filled in with.
/// </summary>
public sealed record ProvisionDemoRequestRequestDto(
    string? OrganizationName,
    string? Slug,
    string? AdminEmail,
    string? Role);
