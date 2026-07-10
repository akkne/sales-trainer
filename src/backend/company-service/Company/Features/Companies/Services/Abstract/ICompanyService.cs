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

    /// <summary>Generates a fresh pre-call briefing via ai-service and caches it on the company. Null if the company doesn't exist/belong to the user.</summary>
    Task<CompanyBriefingDto?> GenerateBriefingAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Returns the cached briefing (both fields null if never generated). Null if the company doesn't exist/belong to the user.</summary>
    Task<CompanyBriefingDto?> GetBriefingAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proxies pasted raw notes/transcript to ai-service to extract a draft call-log entry
    /// (contact, subject, outcome, optional date) for the user to review before saving. Does not
    /// persist anything. Null if the company doesn't exist/belong to the user.
    /// </summary>
    Task<ParsedCallLogDto?> ParseCallLogAsync(Guid userId, Guid companyId, ParseCallLogRequestDto request, CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<CompanyPersonaDto>?> ListPersonasAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyPersonaDto?> CreatePersonaAsync(Guid userId, Guid companyId, CreateCompanyPersonaRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeletePersonaAsync(Guid userId, Guid companyId, Guid personaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Proxies to ai-service to generate a draft buyer persona (name, position, personality) for
    /// a practice call against this company, optionally seeded from a contact's name/position.
    /// Does not persist anything — the caller reviews the result and, if desired, saves it via
    /// <see cref="CreatePersonaAsync"/>. Null if the company doesn't exist/belong to the user.
    /// </summary>
    Task<GeneratedCompanyPersonaDto?> GeneratePersonaAsync(Guid userId, Guid companyId, GenerateCompanyPersonaRequestDto request, CancellationToken cancellationToken = default);
}
