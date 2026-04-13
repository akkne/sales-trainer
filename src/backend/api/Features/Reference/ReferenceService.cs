using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Reference;

internal sealed class ReferenceService(AppDbContext databaseContext) : IReferenceService
{
    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        string skillSlug,
        CancellationToken cancellationToken = default)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(skillRecord => skillRecord.Slug == skillSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Skill '{skillSlug}' not found.");

        return await databaseContext.ReferenceMaterials
            .Where(material => material.SkillId == skill.Id)
            .OrderBy(material => material.SortOrder)
            .Select(material => new ReferenceMaterialDto(
                material.Id,
                material.Title,
                material.MarkdownContent,
                material.SortOrder,
                material.Category,
                material.Tags != null
                    ? material.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>(),
                skill.Slug))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = from material in databaseContext.ReferenceMaterials
                    join skill in databaseContext.Skills
                        on material.SkillId equals skill.Id
                    select new { material, skill };

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(pair => pair.material.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(pair =>
                pair.material.Title.ToLower().Contains(searchLower) ||
                pair.material.MarkdownContent.ToLower().Contains(searchLower));
        }

        var results = await query
            .OrderBy(pair => pair.material.SortOrder)
            .ThenBy(pair => pair.material.Title)
            .ToListAsync(cancellationToken);

        return results.Select(pair => new ReferenceMaterialDto(
            pair.material.Id,
            pair.material.Title,
            pair.material.MarkdownContent,
            pair.material.SortOrder,
            pair.material.Category,
            pair.material.Tags != null
                ? pair.material.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>(),
            pair.skill.Slug))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await databaseContext.ReferenceMaterials
            .Where(material => material.Category != null)
            .Select(material => material.Category!)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);
    }
}
