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

    /// <summary>
    /// Phase 40.18. Set when this row is one organization's copy-on-write override of a global
    /// mode; null otherwise. A row with a parent always has an owning organization — a database
    /// CHECK says so, because a global row shadowing a global row would hide the shared prompt
    /// library behind a copy of itself for every customer at once.
    ///
    /// <para>
    /// The override keeps its parent's <see cref="BundleId"/> and <see cref="Key"/>. That is legal
    /// under the 40.11 unique indexes (the composite one is filtered to non-global rows) and it is
    /// what makes an overridden prompt appear in the same place in the same bundle, with no second
    /// resolution layer for "which folder does this belong to".
    /// </para>
    /// </summary>
    public Guid? ParentModeId { get; set; }

    /// <summary>
    /// Phase 40.18. Lowercase hex SHA-256 of the parent's canonical content at the moment this
    /// override was forked or last reviewed. A prompt has no immutable version table the way a
    /// lesson does (40.15), so the fork point is a fingerprint of the base rather than a pointer at
    /// a frozen snapshot — enough to answer "has upstream moved?", not enough to show what it said
    /// before (docs/DECISIONS.md, 2026-08-18).
    /// </summary>
    public string? BaseContentHash { get; set; }

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
