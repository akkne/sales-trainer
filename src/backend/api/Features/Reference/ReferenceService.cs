using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Reference;

public class ReferenceService(AppDbContext databaseContext)
{
    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        string skillSlug)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(skill => skill.Slug == skillSlug)
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
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search)
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
            .ToListAsync();

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

    public async Task<IReadOnlyList<string>> GetAllCategoriesAsync()
    {
        return await databaseContext.ReferenceMaterials
            .Where(m => m.Category != null)
            .Select(m => m.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }
}
