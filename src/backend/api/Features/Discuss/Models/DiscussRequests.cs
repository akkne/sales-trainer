namespace SalesTrainer.Api.Features.Discuss.Models;

public sealed class CreateThreadRequestDto
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>Mix of existing tag slugs and new free-form labels.</summary>
    public List<string> Tags { get; set; } = [];
}

public sealed class CreateReplyRequestDto
{
    public string Body { get; set; } = "";
}

public sealed class SetAcceptedReplyRequestDto
{
    public Guid ReplyId { get; set; }
}

public sealed class CreateTagRequestDto
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
}

public sealed class UpdateTagRequestDto
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
}

public sealed class SetPinRequestDto
{
    public bool IsPinned { get; set; }
}

public sealed class SetHotRequestDto
{
    public bool IsHot { get; set; }
}

/// <summary>Query parameters for the thread listing endpoint.</summary>
public sealed record DiscussThreadQuery(
    string Sort,
    string? Search,
    string? Tag,
    int Page,
    int PageSize,
    bool IncludeAll);
