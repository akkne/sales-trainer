namespace Sellevate.Company.Features.Companies.Exceptions;

/// <summary>
/// Thrown when a call-log create/update request references a <c>ContactId</c> that does not
/// belong to the target company — either it never did, or it was concurrently deleted between
/// the ownership check and <c>SaveChangesAsync</c> (surfaced there as a FK-violation
/// <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>). Distinct from the generic
/// <see cref="InvalidOperationException"/> family the controller maps to 503 (AI/service-unavailable
/// failures) so this always maps to 400 instead.
/// </summary>
public sealed class ContactNotFoundInCompanyException : Exception
{
    public Guid ContactId { get; }
    public Guid CompanyId { get; }

    public ContactNotFoundInCompanyException(Guid contactId, Guid companyId)
        : base("Указанный контакт не найден в этой компании.")
    {
        ContactId = contactId;
        CompanyId = companyId;
    }

    public ContactNotFoundInCompanyException(Guid contactId, Guid companyId, Exception innerException)
        : base("Указанный контакт не найден в этой компании.", innerException)
    {
        ContactId = contactId;
        CompanyId = companyId;
    }
}
