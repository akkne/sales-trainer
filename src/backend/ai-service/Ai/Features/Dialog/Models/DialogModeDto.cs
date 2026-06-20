namespace Sellevate.Ai.Features.Dialog.Models;

public class DialogModeDto
{
    public Guid Id { get; set; }
    public Guid BundleId { get; set; }
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool VoiceEnabled { get; set; }

    public static DialogModeDto FromEntity(DialogMode mode) => new()
    {
        Id = mode.Id,
        BundleId = mode.BundleId,
        Key = mode.Key,
        Title = mode.Title,
        Description = mode.Description,
        SortOrder = mode.SortOrder,
        IsActive = mode.IsActive,
        VoiceEnabled = mode.VoiceEnabled
    };
}
