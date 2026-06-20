namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogBundle
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DialogMode> Modes { get; set; } = [];
}
