using Sellevate.Ai.Infrastructure.Learning;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogBundleDto
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string SkillSlug { get; set; } = "";
    public string SkillTitle { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsHidden { get; set; }

    /// <summary>
    /// C-3 audit fix. <paramref name="skillLookup"/> comes from <see cref="ISkillLookupClient"/> and
    /// is keyed by <see cref="Models.DialogBundle.SkillId"/>; a skill missing from it (a stale id, or
    /// learning-service unreachable) renders as an empty slug and title rather than failing the whole
    /// bundle list.
    /// </summary>
    public static DialogBundleDto FromEntity(
        DialogBundle bundle, IReadOnlyDictionary<Guid, SkillSummary> skillLookup)
    {
        skillLookup.TryGetValue(bundle.SkillId, out var skill);

        return new()
        {
            Id = bundle.Id,
            SkillId = bundle.SkillId,
            SkillSlug = skill?.Slug ?? "",
            SkillTitle = skill?.Title ?? "",
            Title = bundle.Title,
            Description = bundle.Description,
            IconEmoji = bundle.IconEmoji,
            SortOrder = bundle.SortOrder,
            IsActive = bundle.IsActive,
            IsHidden = bundle.IsHidden
        };
    }
}
