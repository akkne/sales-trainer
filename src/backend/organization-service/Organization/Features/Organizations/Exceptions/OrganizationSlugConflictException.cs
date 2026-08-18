namespace Sellevate.Organization.Features.Organizations.Exceptions;

public sealed class OrganizationSlugConflictException : Exception
{
    public const string Code = "organization_slug_conflict";

    public string Slug { get; }

    public OrganizationSlugConflictException(string slug)
        : base($"An organization with slug '{slug}' already exists.")
    {
        Slug = slug;
    }
}
