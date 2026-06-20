namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogBundleDto
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
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
        SkillTitle = "",
        Title = bundle.Title,
        Description = bundle.Description,
        IconEmoji = bundle.IconEmoji,
        SortOrder = bundle.SortOrder,
        IsActive = bundle.IsActive
    };
}
