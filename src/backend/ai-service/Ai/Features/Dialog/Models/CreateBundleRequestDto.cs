namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class CreateBundleRequestDto
{
    public Guid SkillId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
