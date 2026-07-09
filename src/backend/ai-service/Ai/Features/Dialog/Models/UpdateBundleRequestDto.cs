namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class UpdateBundleRequestDto
{
    public Guid? SkillId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? IconEmoji { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsHidden { get; set; }
}
