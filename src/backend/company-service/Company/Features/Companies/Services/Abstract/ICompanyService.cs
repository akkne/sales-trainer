using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Services.Abstract;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanySummaryDto>> ListCompaniesAsync(Guid userId, string? search, CancellationToken cancellationToken = default);
    Task<CompanyDetailDto> CreateCompanyAsync(Guid userId, CreateCompanyRequestDto request, CancellationToken cancellationToken = default);
    Task<CompanyDetailDto?> GetCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyDetailDto?> UpdateCompanyAsync(Guid userId, Guid companyId, UpdateCompanyRequestDto request, CancellationToken cancellationToken = default);
    Task<CompanyDetailDto?> UpdateCompanyStatusAsync(Guid userId, Guid companyId, UpdateCompanyStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<CompanyDetailDto?> UpdateCompanyFollowUpAsync(Guid userId, Guid companyId, UpdateCompanyFollowUpRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallLogEntryDto>?> ListCallLogEntriesAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<CallLogEntryDto?> CreateCallLogEntryAsync(Guid userId, Guid companyId, CreateCallLogEntryRequestDto request, CancellationToken cancellationToken = default);
    Task<CallLogEntryDto?> UpdateCallLogEntryAsync(Guid userId, Guid companyId, Guid logId, UpdateCallLogEntryRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCallLogEntryAsync(Guid userId, Guid companyId, Guid logId, CancellationToken cancellationToken = default);

    Task<PracticeCallDto?> CreatePracticeCallAsync(Guid userId, Guid companyId, CreatePracticeCallRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PracticeCallDto>?> ListPracticeCallsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>?> GetRecentGoalsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyContactDto>?> ListContactsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyContactDto?> CreateContactAsync(Guid userId, Guid companyId, CreateCompanyContactRequestDto request, CancellationToken cancellationToken = default);
    Task<CompanyContactDto?> UpdateContactAsync(Guid userId, Guid companyId, Guid contactId, UpdateCompanyContactRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteContactAsync(Guid userId, Guid companyId, Guid contactId, CancellationToken cancellationToken = default);
}
