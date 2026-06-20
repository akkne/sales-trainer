using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Reference.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Reference.Services.Implementation;

internal sealed class ReferenceService(LearningDbContext databaseContext) : IReferenceService
{
    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        return await databaseContext.ReferenceMaterials
            .Where(material => material.SkillId == skillId)
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
                material.SkillId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = databaseContext.ReferenceMaterials.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(material => material.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(material =>
                material.Title.ToLower().Contains(searchLower) ||
                material.MarkdownContent.ToLower().Contains(searchLower));
        }

        var results = await query
            .OrderBy(material => material.SortOrder)
            .ThenBy(material => material.Title)
            .ToListAsync(cancellationToken);

        return results.Select(material => new ReferenceMaterialDto(
            material.Id,
            material.Title,
            material.MarkdownContent,
            material.SortOrder,
            material.Category,
            material.Tags != null
                ? material.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>(),
            material.SkillId))
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
