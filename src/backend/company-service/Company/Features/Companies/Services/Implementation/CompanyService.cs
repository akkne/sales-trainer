using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Exceptions;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Infrastructure.Ai;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Features.Companies.Services.Implementation;

/// <summary>
/// Phase 40.12. Company-service's scope is <b>double</b>: a row is visible to one user inside one
/// organization, and the two halves are enforced in two different places on purpose.
///
/// <list type="bullet">
/// <item><b>Organization</b> — never written here. It comes from <c>ITenantContext</c> via the EF
/// query filter in <c>CompanyDbContext</c> and, authoritatively, from the row-level-security policy
/// on all five tables. That is why every method below opens a <see cref="TenantTransactionScope"/>
/// as its first statement: <c>SET LOCAL app.organization_id</c> only applies inside a transaction,
/// and a read outside one returns zero rows under RLS — which looks exactly like an empty CRM.</item>
/// <item><b>User</b> — always an explicit <c>UserId == userId</c> predicate, on the parent row and
/// on every sub-resource query, not inferred from having already checked the parent. A manager and
/// a colleague share an organization, so the organization filter alone would hand one salesperson
/// another's pipeline. The two exceptions are the navigation-property counts in
/// <c>ListCompaniesAsync</c>/<c>GetCompanyAsync</c>, which count children of a company row that
/// was already matched by both halves.</item>
/// </list>
///
/// An id that exists but fails either half is indistinguishable from an id that does not exist —
/// both are <c>404</c>, never <c>403</c>, which is the pre-existing rule extended to the new half.
///
/// <para>
/// <b>Two caches live on the company row rather than in a cache store,</b> both for the readiness
/// score. The positive one is <c>ReadinessJson</c>; the negative one, <c>ReadinessNoFeedbackUntil</c>,
/// records that a fan-out already came back with nothing usable, so repeated reads inside its TTL do
/// not re-run up to <see cref="MaxSessionIdsForReadiness"/> sequential Mongo reads in ai-service. Its
/// TTL (<see cref="CompanyServiceOptions.ReadinessNoFeedbackCacheMinutes"/>) is short because
/// feedback can land at any moment, and <see cref="CreatePracticeCallAsync"/>
/// clears both eagerly, since a new practice call is this codebase's practice-completion signal
/// (39.16). A cache value that fails to deserialize — hand-edited, or a literal <c>null</c> — counts
/// as a miss and is regenerated rather than thrown.
/// </para>
///
/// <para>
/// <b>Follow-up edits only re-arm a reminder when the due date itself changes.</b> Editing the note,
/// or re-submitting the same date, must leave <c>FollowUpNotifiedAt</c> alone: republishing
/// <c>company.followup.due</c> for an already-notified date produces a duplicate notification the
/// consumer cannot dedupe once its original entry has scrolled out of the inbox (it dedupes on
/// companyId + dueDate). Clearing the follow-up clears the note and the marker with it — there is
/// nothing left to remind about.
/// </para>
/// </summary>
internal sealed class CompanyService(
    CompanyDbContext databaseContext,
    IBriefingAiClient briefingAiClient,
    IParseLogAiClient parseLogAiClient,
    IPersonaAiClient personaAiClient,
    IReadinessAiClient readinessAiClient,
    IOptions<CompanyServiceOptions> options) : ICompanyService
{
    /// <summary>
    /// Mirrors ai-service's <c>ReadinessController.MaxSessionIds</c> guard: sending more session ids
    /// than it accepts would be rejected wholesale rather than truncated.
    /// </summary>
    private const int MaxSessionIdsForReadiness = 50;

    private static readonly JsonSerializerOptions ReadinessCacheSerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record ReadinessCachePayload(int Score, List<string> Strengths, List<string> Gaps, string Recommendation);

    public async Task<IReadOnlyList<CompanySummaryDto>> ListCompaniesAsync(
        Guid userId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var query = databaseContext.Companies
            .Where(company => company.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(company => company.Name.ToLower().Contains(normalizedSearch));
        }

        var descriptionExcerptLength = options.Value.DescriptionExcerptLength;

        var companies = await query
            .OrderByDescending(company => company.UpdatedAt)
            .Select(company => new
            {
                company.Id,
                company.Name,
                company.Description,
                company.Status,
                company.NextActionAt,
                company.CreatedAt,
                company.UpdatedAt,
                CallLogCount = company.CallLogEntries.Count,
                PracticeCallCount = company.PracticeCalls.Count,
                ContactCount = company.Contacts.Count
            })
            .ToListAsync(cancellationToken);

        return companies
            .Select(company => new CompanySummaryDto(
                company.Id,
                company.Name,
                company.Description.Length > descriptionExcerptLength
                    ? company.Description[..descriptionExcerptLength]
                    : company.Description,
                company.Status,
                company.CallLogCount,
                company.PracticeCallCount,
                company.ContactCount,
                company.NextActionAt,
                company.CreatedAt,
                company.UpdatedAt))
            .ToList();
    }

    public async Task<CompanyDetailDto> CreateCompanyAsync(
        Guid userId,
        CreateCompanyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var now = DateTime.UtcNow;
        var company = new CompanyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.Companies.Add(company);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToDetailDto(company, 0, 0, 0);
    }

    public async Task<CompanyDetailDto?> UpdateCompanyStatusAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        if (request.Status is not { } status)
            throw new ArgumentException(CompanyErrorMessages.CompanyStatusRequired, nameof(request));

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        company.Status = status;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId && entry.UserId == userId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId && contact.UserId == userId, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<CompanyDetailDto?> UpdateCompanyFollowUpAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyFollowUpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        if (request.NextActionAt is { } nextActionAt)
        {
            var normalizedNextActionAt = nextActionAt.ToUniversalTime();

            if (company.NextActionAt != normalizedNextActionAt)
            {
                company.FollowUpNotifiedAt = null;
            }

            company.NextActionAt = normalizedNextActionAt;
            company.NextActionNote = request.NextActionNote ?? string.Empty;
        }
        else
        {
            company.NextActionAt = null;
            company.NextActionNote = null;
            company.FollowUpNotifiedAt = null;
        }

        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId && entry.UserId == userId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId && contact.UserId == userId, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<CompanyDetailDto?> GetCompanyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.Status,
                c.NextActionAt,
                c.NextActionNote,
                c.FollowUpNotifiedAt,
                c.CreatedAt,
                c.UpdatedAt,
                CallLogCount = c.CallLogEntries.Count,
                PracticeCallCount = c.PracticeCalls.Count,
                ContactCount = c.Contacts.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        return new CompanyDetailDto(
            company.Id,
            company.Name,
            company.Description,
            company.Status,
            company.CallLogCount,
            company.PracticeCallCount,
            company.ContactCount,
            company.NextActionAt,
            company.NextActionNote,
            company.FollowUpNotifiedAt,
            company.CreatedAt,
            company.UpdatedAt);
    }

    public async Task<CompanyDetailDto?> UpdateCompanyAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        company.Name = request.Name;
        company.Description = request.Description ?? string.Empty;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId && entry.UserId == userId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId && contact.UserId == userId, cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<bool> DeleteCompanyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return false;

        databaseContext.Companies.Remove(company);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<CallLogEntryDto>?> ListCallLogEntriesAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CallLogEntries
            .Where(entry => entry.CompanyId == companyId && entry.UserId == userId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => MapToCallLogDto(entry))
            .ToListAsync(cancellationToken);
    }

    public async Task<CallLogEntryDto?> CreateCallLogEntryAsync(
        Guid userId,
        Guid companyId,
        CreateCallLogEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        if (request.ContactId is { } contactId)
            await EnsureContactBelongsToCompanyAsync(userId, companyId, contactId, cancellationToken);

        var now = DateTime.UtcNow;
        var entry = new CallLogEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            ContactId = request.ContactId,
            ContactName = request.ContactName,
            Subject = request.Subject,
            Outcome = request.Outcome,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.CallLogEntries.Add(entry);

        company.UpdatedAt = now;

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbUpdateException) when (
            request.ContactId is { } raceContactId && IsContactIdForeignKeyViolation(dbUpdateException))
        {
            throw new ContactNotFoundInCompanyException(raceContactId, companyId, dbUpdateException);
        }

        await scope.CommitAsync(cancellationToken);

        return MapToCallLogDto(entry);
    }

    public async Task<CallLogEntryDto?> UpdateCallLogEntryAsync(
        Guid userId,
        Guid companyId,
        Guid logId,
        UpdateCallLogEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var entry = await databaseContext.CallLogEntries
            .Where(e => e.Id == logId && e.CompanyId == companyId && e.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
            return null;

        if (request.ContactId is { } contactId)
            await EnsureContactBelongsToCompanyAsync(userId, companyId, contactId, cancellationToken);

        entry.ContactId = request.ContactId;
        entry.ContactName = request.ContactName;
        entry.Subject = request.Subject;
        entry.Outcome = request.Outcome;
        entry.OccurredAt = request.OccurredAt.ToUniversalTime();
        entry.UpdatedAt = DateTime.UtcNow;

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbUpdateException) when (
            request.ContactId is { } raceContactId && IsContactIdForeignKeyViolation(dbUpdateException))
        {
            throw new ContactNotFoundInCompanyException(raceContactId, companyId, dbUpdateException);
        }

        await scope.CommitAsync(cancellationToken);

        return MapToCallLogDto(entry);
    }

    public async Task<bool> DeleteCallLogEntryAsync(
        Guid userId,
        Guid companyId,
        Guid logId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var entry = await databaseContext.CallLogEntries
            .Where(e => e.Id == logId && e.CompanyId == companyId && e.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
            return false;

        databaseContext.CallLogEntries.Remove(entry);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<PracticeCallDto?> CreatePracticeCallAsync(
        Guid userId,
        Guid companyId,
        CreatePracticeCallRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        var practiceCall = new PracticeCall
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            DialogSessionId = request.DialogSessionId,
            Goal = request.Goal ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.PracticeCalls.Add(practiceCall);

        company.ReadinessJson = null;
        company.ReadinessGeneratedAt = null;
        company.ReadinessNoFeedbackUntil = null;

        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToPracticeCallDto(practiceCall);
    }

    public async Task<IReadOnlyList<PracticeCallDto>?> ListPracticeCallsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => MapToPracticeCallDto(practiceCall))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>?> GetRecentGoalsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId && practiceCall.Goal != string.Empty)
            .GroupBy(practiceCall => practiceCall.Goal)
            .Select(group => new { Goal = group.Key, LastCreatedAt = group.Max(practiceCall => practiceCall.CreatedAt) })
            .OrderByDescending(goalEntry => goalEntry.LastCreatedAt)
            .Take(options.Value.RecentGoalCount)
            .Select(goalEntry => goalEntry.Goal)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyContactDto>?> ListContactsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CompanyContacts
            .Where(contact => contact.CompanyId == companyId && contact.UserId == userId)
            .OrderByDescending(contact => contact.CreatedAt)
            .Select(contact => MapToContactDto(contact))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyContactDto?> CreateContactAsync(
        Guid userId,
        Guid companyId,
        CreateCompanyContactRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var now = DateTime.UtcNow;
        var contact = new CompanyContact
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Name = request.Name,
            Position = request.Position ?? string.Empty,
            Notes = request.Notes ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.CompanyContacts.Add(contact);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToContactDto(contact);
    }

    public async Task<CompanyContactDto?> UpdateContactAsync(
        Guid userId,
        Guid companyId,
        Guid contactId,
        UpdateCompanyContactRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var contact = await databaseContext.CompanyContacts
            .Where(c => c.Id == contactId && c.CompanyId == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return null;

        contact.Name = request.Name;
        contact.Position = request.Position ?? string.Empty;
        contact.Notes = request.Notes ?? string.Empty;
        contact.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToContactDto(contact);
    }

    public async Task<bool> DeleteContactAsync(
        Guid userId,
        Guid companyId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var contact = await databaseContext.CompanyContacts
            .Where(c => c.Id == contactId && c.CompanyId == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return false;

        databaseContext.CompanyContacts.Remove(contact);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<CompanyBriefingDto?> GenerateBriefingAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        var latestGoal = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId && practiceCall.Goal != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => practiceCall.Goal)
            .FirstOrDefaultAsync(cancellationToken);

        var recentCalls = await databaseContext.CallLogEntries
            .Where(entry => entry.CompanyId == companyId && entry.UserId == userId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(options.Value.RecentCallLogCountForBriefing)
            .Select(entry => new BriefingCallLogItem(entry.ContactName, entry.Subject, entry.Outcome, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        var aiRequest = new BriefingAiRequest(company.Description, latestGoal, recentCalls, []);
        var aiResult = await briefingAiClient.GenerateBriefingAsync(aiRequest, cancellationToken);

        company.BriefingContent = aiResult.Content;
        company.BriefingGeneratedAt = aiResult.GeneratedAt;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return new CompanyBriefingDto(company.BriefingContent, company.BriefingGeneratedAt);
    }

    public async Task<CompanyBriefingDto?> GetBriefingAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .Select(c => new { c.BriefingContent, c.BriefingGeneratedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        return new CompanyBriefingDto(company.BriefingContent, company.BriefingGeneratedAt);
    }

    public async Task<CompanyReadinessDto?> GetReadinessAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        if (company.ReadinessJson is not null)
        {
            var cached = JsonSerializer.Deserialize<ReadinessCachePayload>(company.ReadinessJson, ReadinessCacheSerializerOptions);
            if (cached is not null)
                return new CompanyReadinessDto(cached.Score, cached.Strengths, cached.Gaps, cached.Recommendation, company.ReadinessGeneratedAt);
        }

        if (company.ReadinessNoFeedbackUntil is { } noFeedbackUntil && noFeedbackUntil > DateTime.UtcNow)
            return new CompanyReadinessDto(null, null, null, null, null);

        var sessionIds = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId && practiceCall.DialogSessionId != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Take(MaxSessionIdsForReadiness)
            .Select(practiceCall => practiceCall.DialogSessionId)
            .ToListAsync(cancellationToken);

        if (sessionIds.Count == 0)
            return new CompanyReadinessDto(null, null, null, null, null);

        var latestGoal = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.UserId == userId && practiceCall.Goal != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => practiceCall.Goal)
            .FirstOrDefaultAsync(cancellationToken);

        var aiResult = await readinessAiClient.GenerateReadinessAsync(
            new ReadinessAiRequest(userId, latestGoal, sessionIds), cancellationToken);

        if (aiResult is null)
        {
            company.ReadinessNoFeedbackUntil =
                DateTime.UtcNow.AddMinutes(options.Value.ReadinessNoFeedbackCacheMinutes);
            company.UpdatedAt = DateTime.UtcNow;

            await databaseContext.SaveChangesAsync(cancellationToken);

            await scope.CommitAsync(cancellationToken);

            return new CompanyReadinessDto(null, null, null, null, null);
        }

        var generatedAt = DateTime.UtcNow;
        company.ReadinessJson = JsonSerializer.Serialize(
            new ReadinessCachePayload(aiResult.Score, aiResult.Strengths, aiResult.Gaps, aiResult.Recommendation),
            ReadinessCacheSerializerOptions);
        company.ReadinessGeneratedAt = generatedAt;
        company.ReadinessNoFeedbackUntil = null;
        company.UpdatedAt = generatedAt;

        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return new CompanyReadinessDto(aiResult.Score, aiResult.Strengths, aiResult.Gaps, aiResult.Recommendation, generatedAt);
    }

    public async Task<ParsedCallLogDto?> ParseCallLogAsync(
        Guid userId,
        Guid companyId,
        ParseCallLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var aiResult = await parseLogAiClient.ParseLogAsync(
            new ParseLogAiRequest(request.RawText), cancellationToken);

        return new ParsedCallLogDto(aiResult.ContactName, aiResult.Subject, aiResult.Outcome, aiResult.OccurredAt);
    }

    public async Task<IReadOnlyList<CompanyPersonaDto>?> ListPersonasAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CompanyPersonas
            .Where(persona => persona.CompanyId == companyId && persona.UserId == userId)
            .OrderByDescending(persona => persona.CreatedAt)
            .Select(persona => MapToPersonaDto(persona))
            .ToListAsync(cancellationToken);
    }

    public async Task<CompanyPersonaDto?> CreatePersonaAsync(
        Guid userId,
        Guid companyId,
        CreateCompanyPersonaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var persona = new CompanyPersona
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Name = request.Name,
            Position = request.Position,
            Personality = request.Personality,
            Difficulty = request.Difficulty,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.CompanyPersonas.Add(persona);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await scope.CommitAsync(cancellationToken);

        return MapToPersonaDto(persona);
    }

    public async Task<bool> DeletePersonaAsync(
        Guid userId,
        Guid companyId,
        Guid personaId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var persona = await databaseContext.CompanyPersonas
            .Where(p => p.Id == personaId && p.CompanyId == companyId && p.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (persona is null)
            return false;

        databaseContext.CompanyPersonas.Remove(persona);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<GeneratedCompanyPersonaDto?> GeneratePersonaAsync(
        Guid userId,
        Guid companyId,
        GenerateCompanyPersonaRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .Select(c => new { c.Description })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        var aiResult = await personaAiClient.GeneratePersonaAsync(
            new PersonaAiRequest(company.Description, request.ContactName, request.ContactPosition, request.Difficulty.ToString()),
            cancellationToken);

        return new GeneratedCompanyPersonaDto(aiResult.Name, aiResult.Position, aiResult.Personality);
    }

    private async Task EnsureContactBelongsToCompanyAsync(Guid userId, Guid companyId, Guid contactId, CancellationToken cancellationToken)
    {
        var contactBelongsToCompany = await databaseContext.CompanyContacts
            .AnyAsync(contact => contact.Id == contactId && contact.CompanyId == companyId && contact.UserId == userId, cancellationToken);

        if (!contactBelongsToCompany)
            throw new ContactNotFoundInCompanyException(contactId, companyId);
    }

    private const string ContactIdForeignKeyConstraintName = "FK_CallLogEntries_CompanyContacts_ContactId";

    /// <summary>
    /// True only for a Postgres foreign-key violation (<c>23503</c>) against exactly the
    /// <c>CallLogEntries.ContactId -> CompanyContacts.Id</c> constraint, which is how the concurrent
    /// contact-delete race surfaces: the ownership check and <c>SaveChangesAsync</c> are not atomic,
    /// so a contact can vanish between them.
    ///
    /// <para>
    /// The constraint name is matched, not just the SQL state, because every other
    /// <c>DbUpdateException</c> — an unrelated unique violation, a deadlock, a dropped connection —
    /// must keep propagating as a 500. Mis-mapping one of those to "contact not found" would hide a
    /// real database failure behind a 400 that looks like the user's fault.
    /// </para>
    /// </summary>
    private static bool IsContactIdForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } postgresException &&
        string.Equals(postgresException.ConstraintName, ContactIdForeignKeyConstraintName, StringComparison.Ordinal);

    private static CompanyDetailDto MapToDetailDto(CompanyEntity company, int callLogCount, int practiceCallCount, int contactCount) =>
        new(
            company.Id,
            company.Name,
            company.Description,
            company.Status,
            callLogCount,
            practiceCallCount,
            contactCount,
            company.NextActionAt,
            company.NextActionNote,
            company.FollowUpNotifiedAt,
            company.CreatedAt,
            company.UpdatedAt);

    private static CallLogEntryDto MapToCallLogDto(CallLogEntry entry) =>
        new(entry.Id, entry.CompanyId, entry.ContactName, entry.Subject, entry.Outcome, entry.OccurredAt, entry.CreatedAt, entry.UpdatedAt, entry.ContactId);

    private static PracticeCallDto MapToPracticeCallDto(PracticeCall practiceCall) =>
        new(practiceCall.Id, practiceCall.CompanyId, practiceCall.DialogSessionId, practiceCall.Goal, practiceCall.CreatedAt);

    private static CompanyContactDto MapToContactDto(CompanyContact contact) =>
        new(contact.Id, contact.CompanyId, contact.Name, contact.Position, contact.Notes, contact.CreatedAt, contact.UpdatedAt);

    private static CompanyPersonaDto MapToPersonaDto(CompanyPersona persona) =>
        new(persona.Id, persona.CompanyId, persona.Name, persona.Position, persona.Personality, persona.Difficulty, persona.CreatedAt);
}
