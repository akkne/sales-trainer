using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Infrastructure.Ai;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Features.Companies.Services.Implementation;

internal sealed class CompanyService(
    CompanyDbContext databaseContext,
    IBriefingAiClient briefingAiClient,
    IParseLogAiClient parseLogAiClient,
    IPersonaAiClient personaAiClient,
    IReadinessAiClient readinessAiClient) : ICompanyService
{
    private const int DescriptionExcerptLength = 160;
    private const int RecentGoalCount = 5;
    private const int RecentCallLogCountForBriefing = 5;

    // Mirrors ai-service's ReadinessController.MaxSessionIds guard — no point sending more
    // session ids than ai-service will accept.
    private const int MaxSessionIdsForReadiness = 50;

    // How long to negative-cache a "no usable feedback yet" readiness result (ai-service fanned
    // out to Mongo across the company's practice sessions and found no feedback text). Without
    // this, every GET re-runs the fan-out (up to MaxSessionIdsForReadiness sequential Mongo
    // reads) until feedback lands. Short TTL because feedback can land at any time (practice
    // call creation already invalidates this eagerly — see CreatePracticeCallAsync).
    private static readonly TimeSpan ReadinessNoFeedbackCacheTtl = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions ReadinessCacheSerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record ReadinessCachePayload(int Score, List<string> Strengths, List<string> Gaps, string Recommendation);

    public async Task<IReadOnlyList<CompanySummaryDto>> ListCompaniesAsync(
        Guid userId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = databaseContext.Companies
            .Where(company => company.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(company => company.Name.ToLower().Contains(normalizedSearch));
        }

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
                company.Description.Length > DescriptionExcerptLength
                    ? company.Description[..DescriptionExcerptLength]
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

        return MapToDetailDto(company, 0, 0, 0);
    }

    public async Task<CompanyDetailDto?> UpdateCompanyStatusAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status is not { } status)
            throw new ArgumentException("Status is required.", nameof(request));

        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        company.Status = status;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId, cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<CompanyDetailDto?> UpdateCompanyFollowUpAsync(
        Guid userId,
        Guid companyId,
        UpdateCompanyFollowUpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        if (request.NextActionAt is { } nextActionAt)
        {
            var normalizedNextActionAt = nextActionAt.ToUniversalTime();

            // Only reset FollowUpNotifiedAt when the due date actually changes, so the reminder
            // background service notifies again for a genuinely new due date. Editing only the
            // note (or re-submitting the same date) must NOT re-arm an already-fired reminder —
            // otherwise the next poll would republish company.followup.due for a date that was
            // already notified, producing a spurious duplicate notification once the original
            // scrolls out of the recipient's inbox (the consumer dedupes on companyId+dueDate,
            // which can no longer catch a same-date replay once that entry has expired/scrolled).
            if (company.NextActionAt != normalizedNextActionAt)
            {
                company.FollowUpNotifiedAt = null;
            }

            company.NextActionAt = normalizedNextActionAt;
            company.NextActionNote = request.NextActionNote ?? string.Empty;
        }
        else
        {
            // Clearing the follow-up clears the note and the notified-at marker with it —
            // there is nothing left to remind about.
            company.NextActionAt = null;
            company.NextActionNote = null;
            company.FollowUpNotifiedAt = null;
        }

        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId, cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<CompanyDetailDto?> GetCompanyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
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
            .CountAsync(entry => entry.CompanyId == companyId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId, cancellationToken);
        var contactCount = await databaseContext.CompanyContacts
            .CountAsync(contact => contact.CompanyId == companyId, cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount, contactCount);
    }

    public async Task<bool> DeleteCompanyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await databaseContext.Companies
            .Where(c => c.Id == companyId && c.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return false;

        databaseContext.Companies.Remove(company);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CallLogEntryDto>?> ListCallLogEntriesAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CallLogEntries
            .Where(entry => entry.CompanyId == companyId)
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
        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        if (request.ContactId is { } contactId)
            await EnsureContactBelongsToCompanyAsync(companyId, contactId, cancellationToken);

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

        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToCallLogDto(entry);
    }

    public async Task<CallLogEntryDto?> UpdateCallLogEntryAsync(
        Guid userId,
        Guid companyId,
        Guid logId,
        UpdateCallLogEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var entry = await databaseContext.CallLogEntries
            .Where(e => e.Id == logId && e.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
            return null;

        if (request.ContactId is { } contactId)
            await EnsureContactBelongsToCompanyAsync(companyId, contactId, cancellationToken);

        entry.ContactId = request.ContactId;
        entry.ContactName = request.ContactName;
        entry.Subject = request.Subject;
        entry.Outcome = request.Outcome;
        entry.OccurredAt = request.OccurredAt.ToUniversalTime();
        entry.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToCallLogDto(entry);
    }

    public async Task<bool> DeleteCallLogEntryAsync(
        Guid userId,
        Guid companyId,
        Guid logId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var entry = await databaseContext.CallLogEntries
            .Where(e => e.Id == logId && e.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
            return false;

        databaseContext.CallLogEntries.Remove(entry);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PracticeCallDto?> CreatePracticeCallAsync(
        Guid userId,
        Guid companyId,
        CreatePracticeCallRequestDto request,
        CancellationToken cancellationToken = default)
    {
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
            Goal = request.Goal,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.PracticeCalls.Add(practiceCall);

        // A new practice call is this codebase's practice-completion signal (see 39.16 design) —
        // invalidate the cached readiness score so the next GET regenerates it from fresh feedback.
        // Also clear the negative "no usable feedback yet" cache — this practice call may be the
        // one that finally has usable feedback.
        company.ReadinessJson = null;
        company.ReadinessGeneratedAt = null;
        company.ReadinessNoFeedbackUntil = null;

        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToPracticeCallDto(practiceCall);
    }

    public async Task<IReadOnlyList<PracticeCallDto>?> ListPracticeCallsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => MapToPracticeCallDto(practiceCall))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>?> GetRecentGoalsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.Goal != string.Empty)
            .GroupBy(practiceCall => practiceCall.Goal)
            .Select(group => new { Goal = group.Key, LastCreatedAt = group.Max(practiceCall => practiceCall.CreatedAt) })
            .OrderByDescending(goalEntry => goalEntry.LastCreatedAt)
            .Take(RecentGoalCount)
            .Select(goalEntry => goalEntry.Goal)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyContactDto>?> ListContactsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CompanyContacts
            .Where(contact => contact.CompanyId == companyId)
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

        return MapToContactDto(contact);
    }

    public async Task<CompanyContactDto?> UpdateContactAsync(
        Guid userId,
        Guid companyId,
        Guid contactId,
        UpdateCompanyContactRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var contact = await databaseContext.CompanyContacts
            .Where(c => c.Id == contactId && c.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return null;

        contact.Name = request.Name;
        contact.Position = request.Position;
        contact.Notes = request.Notes;
        contact.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToContactDto(contact);
    }

    public async Task<bool> DeleteContactAsync(
        Guid userId,
        Guid companyId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var contact = await databaseContext.CompanyContacts
            .Where(c => c.Id == contactId && c.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (contact is null)
            return false;

        databaseContext.CompanyContacts.Remove(contact);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyBriefingDto?> GenerateBriefingAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        // Most recent non-empty practice-call goal, if any — same "latest goal" notion as the
        // recent-goals feature, but only the single newest one (not the last-5-distinct list).
        var latestGoal = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.Goal != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => practiceCall.Goal)
            .FirstOrDefaultAsync(cancellationToken);

        var recentCalls = await databaseContext.CallLogEntries
            .Where(entry => entry.CompanyId == companyId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(RecentCallLogCountForBriefing)
            .Select(entry => new BriefingCallLogItem(entry.ContactName, entry.Subject, entry.Outcome, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        // Practice-session feedback text lives in ai-service's Mongo store, not here — company-service
        // has no cross-service read for it (out of scope for 39.12), so the feedback-summaries list is
        // always empty; ai-service's briefing prompt degrades gracefully when it's empty.
        var aiRequest = new BriefingAiRequest(company.Description, latestGoal, recentCalls, []);
        var aiResult = await briefingAiClient.GenerateBriefingAsync(aiRequest, cancellationToken);

        company.BriefingContent = aiResult.Content;
        company.BriefingGeneratedAt = aiResult.GeneratedAt;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new CompanyBriefingDto(company.BriefingContent, company.BriefingGeneratedAt);
    }

    public async Task<CompanyBriefingDto?> GetBriefingAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
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
        var company = await databaseContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.UserId == userId, cancellationToken);

        if (company is null)
            return null;

        if (company.ReadinessJson is not null)
        {
            var cached = JsonSerializer.Deserialize<ReadinessCachePayload>(company.ReadinessJson, ReadinessCacheSerializerOptions);
            // A corrupted/hand-edited cache value (e.g. literal "null") is treated as a
            // cache miss and regenerated below, rather than throwing a raw 500.
            if (cached is not null)
                return new CompanyReadinessDto(cached.Score, cached.Strengths, cached.Gaps, cached.Recommendation, company.ReadinessGeneratedAt);
        }

        // Negative cache: if a recent fan-out already came back with no usable feedback and the
        // TTL hasn't expired yet, skip straight to the empty result instead of re-running the
        // (up to MaxSessionIdsForReadiness) sequential Mongo reads on every request.
        if (company.ReadinessNoFeedbackUntil is { } noFeedbackUntil && noFeedbackUntil > DateTime.UtcNow)
            return new CompanyReadinessDto(null, null, null, null, null);

        // Most recent practice-call session ids first (capped to what ai-service accepts), plus
        // the single latest non-empty goal — same "latest goal" notion used by the briefing feature.
        var sessionIds = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.DialogSessionId != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Take(MaxSessionIdsForReadiness)
            .Select(practiceCall => practiceCall.DialogSessionId)
            .ToListAsync(cancellationToken);

        if (sessionIds.Count == 0)
            return new CompanyReadinessDto(null, null, null, null, null);

        var latestGoal = await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.Goal != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => practiceCall.Goal)
            .FirstOrDefaultAsync(cancellationToken);

        var aiResult = await readinessAiClient.GenerateReadinessAsync(
            new ReadinessAiRequest(userId, latestGoal, sessionIds), cancellationToken);

        if (aiResult is null)
        {
            // No usable feedback found across the fanned-out sessions — negative-cache the result
            // so repeated requests within the TTL don't re-fan-out. CreatePracticeCallAsync still
            // clears this eagerly when a new practice call completes.
            company.ReadinessNoFeedbackUntil = DateTime.UtcNow.Add(ReadinessNoFeedbackCacheTtl);
            company.UpdatedAt = DateTime.UtcNow;

            await databaseContext.SaveChangesAsync(cancellationToken);

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

        return new CompanyReadinessDto(aiResult.Score, aiResult.Strengths, aiResult.Gaps, aiResult.Recommendation, generatedAt);
    }

    public async Task<ParsedCallLogDto?> ParseCallLogAsync(
        Guid userId,
        Guid companyId,
        ParseCallLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
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
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        return await databaseContext.CompanyPersonas
            .Where(persona => persona.CompanyId == companyId)
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

        return MapToPersonaDto(persona);
    }

    public async Task<bool> DeletePersonaAsync(
        Guid userId,
        Guid companyId,
        Guid personaId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return false;

        var persona = await databaseContext.CompanyPersonas
            .Where(p => p.Id == personaId && p.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (persona is null)
            return false;

        databaseContext.CompanyPersonas.Remove(persona);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GeneratedCompanyPersonaDto?> GeneratePersonaAsync(
        Guid userId,
        Guid companyId,
        GenerateCompanyPersonaRequestDto request,
        CancellationToken cancellationToken = default)
    {
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

    private async Task EnsureContactBelongsToCompanyAsync(Guid companyId, Guid contactId, CancellationToken cancellationToken)
    {
        var contactBelongsToCompany = await databaseContext.CompanyContacts
            .AnyAsync(contact => contact.Id == contactId && contact.CompanyId == companyId, cancellationToken);

        if (!contactBelongsToCompany)
            throw new InvalidOperationException("Указанный контакт не найден в этой компании.");
    }

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
