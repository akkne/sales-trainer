namespace SalesTrainer.Api.Features.Dialog;

public class StartSessionRequestDto
{
    public Guid BundleId { get; set; }
    public Guid ModeId { get; set; }
}

public class SendMessageRequestDto
{
    public string Content { get; set; } = null!;
}

public class CreateBundleRequestDto
{
    public Guid SkillId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateBundleRequestDto
{
    public Guid? SkillId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? IconEmoji { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateModeRequestDto
{
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string ChatSystemPrompt { get; set; } = null!;
    public string FeedbackSystemPrompt { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateModeRequestDto
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ChatSystemPrompt { get; set; }
    public string? FeedbackSystemPrompt { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}
