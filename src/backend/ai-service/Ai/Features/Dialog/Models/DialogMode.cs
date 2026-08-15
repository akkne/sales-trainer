namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogMode
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.11. <see langword="null"/> means a globally shared mode; a non-null value means an
    /// organization authored it. The organization is part of the mode's identity: <see cref="Key"/>
    /// is unique per <c>(OrganizationId, BundleId)</c>, with a separate partial unique index over
    /// the global rows, so two organizations may both define a mode called <c>discovery-call</c>
    /// without colliding with each other or with the global library.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid BundleId { get; set; }
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string ChatSystemPrompt { get; set; } = null!;
    public string FeedbackSystemPrompt { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool VoiceEnabled { get; set; }
    public string? VoiceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DialogBundle Bundle { get; set; } = null!;
}
