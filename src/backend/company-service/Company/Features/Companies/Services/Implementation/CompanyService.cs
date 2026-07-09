using Microsoft.EntityFrameworkCore;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Features.Companies.Services.Implementation;

internal sealed class CompanyService(CompanyDbContext databaseContext) : ICompanyService
{
    private const int DescriptionExcerptLength = 160;
    private const int RecentGoalCount = 5;

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
                company.CreatedAt,
                company.UpdatedAt,
                CallLogCount = company.CallLogEntries.Count,
                PracticeCallCount = company.PracticeCalls.Count
            })
            .ToListAsync(cancellationToken);

        return companies
            .Select(company => new CompanySummaryDto(
                company.Id,
                company.Name,
                company.Description.Length > DescriptionExcerptLength
                    ? company.Description[..DescriptionExcerptLength]
                    : company.Description,
                company.CallLogCount,
                company.PracticeCallCount,
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
            Description = string.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.Companies.Add(company);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToDetailDto(company, 0, 0);
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
                c.CreatedAt,
                c.UpdatedAt,
                CallLogCount = c.CallLogEntries.Count,
                PracticeCallCount = c.PracticeCalls.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
            return null;

        return new CompanyDetailDto(
            company.Id,
            company.Name,
            company.Description,
            company.CallLogCount,
            company.PracticeCallCount,
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
        company.Description = request.Description;
        company.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        var callLogCount = await databaseContext.CallLogEntries
            .CountAsync(entry => entry.CompanyId == companyId, cancellationToken);
        var practiceCallCount = await databaseContext.PracticeCalls
            .CountAsync(practiceCall => practiceCall.CompanyId == companyId, cancellationToken);

        return MapToDetailDto(company, callLogCount, practiceCallCount);
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

    public async Task<IReadOnlyList<CallLogEntryDto>> ListCallLogEntriesAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return [];

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
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return null;

        var now = DateTime.UtcNow;
        var entry = new CallLogEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            ContactName = request.ContactName,
            Subject = request.Subject,
            Outcome = request.Outcome,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.CallLogEntries.Add(entry);

        var company = await databaseContext.Companies.FindAsync([companyId], cancellationToken);
        if (company is not null)
        {
            company.UpdatedAt = now;
        }

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
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
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
        await databaseContext.SaveChangesAsync(cancellationToken);

        return MapToPracticeCallDto(practiceCall);
    }

    public async Task<IReadOnlyList<PracticeCallDto>> ListPracticeCallsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return [];

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => MapToPracticeCallDto(practiceCall))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRecentGoalsAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await databaseContext.Companies
            .AnyAsync(company => company.Id == companyId && company.UserId == userId, cancellationToken);

        if (!companyExists)
            return [];

        return await databaseContext.PracticeCalls
            .Where(practiceCall => practiceCall.CompanyId == companyId && practiceCall.Goal != string.Empty)
            .OrderByDescending(practiceCall => practiceCall.CreatedAt)
            .Select(practiceCall => practiceCall.Goal)
            .Distinct()
            .Take(RecentGoalCount)
            .ToListAsync(cancellationToken);
    }

    private static CompanyDetailDto MapToDetailDto(CompanyEntity company, int callLogCount, int practiceCallCount) =>
        new(company.Id, company.Name, company.Description, callLogCount, practiceCallCount, company.CreatedAt, company.UpdatedAt);

    private static CallLogEntryDto MapToCallLogDto(CallLogEntry entry) =>
        new(entry.Id, entry.CompanyId, entry.ContactName, entry.Subject, entry.Outcome, entry.OccurredAt, entry.CreatedAt, entry.UpdatedAt);

    private static PracticeCallDto MapToPracticeCallDto(PracticeCall practiceCall) =>
        new(practiceCall.Id, practiceCall.CompanyId, practiceCall.DialogSessionId, practiceCall.Goal, practiceCall.CreatedAt);
}
