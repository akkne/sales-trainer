namespace SalesTrainer.Api.Features.Dialog;

public class DialogBundleDto
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string SkillSlug { get; set; } = null!;
    public string SkillTitle { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public static DialogBundleDto FromEntity(DialogBundle bundle) => new()
    {
        Id = bundle.Id,
        SkillId = bundle.SkillId,
        SkillSlug = bundle.Skill?.Slug ?? "",
        SkillTitle = bundle.Skill?.Title ?? "",
        Title = bundle.Title,
        Description = bundle.Description,
        IconEmoji = bundle.IconEmoji,
        SortOrder = bundle.SortOrder,
        IsActive = bundle.IsActive
    };
}
