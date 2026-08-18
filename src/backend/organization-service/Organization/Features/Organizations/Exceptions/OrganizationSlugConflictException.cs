namespace Sellevate.Organization.Features.Organizations.Exceptions;

/// <summary>
/// The requested slug is already in use. Rendered as 409 with <see cref="Code"/> in the body so a
/// client can tell «this name is taken» apart from every other rejection without parsing the message —
/// the code is the contract, the sentence is not.
/// </summary>
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
