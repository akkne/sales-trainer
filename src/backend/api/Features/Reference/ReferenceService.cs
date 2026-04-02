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
                material.SortOrder))
            .ToListAsync();
    }
}
