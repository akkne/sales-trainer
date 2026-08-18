namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>Phase 40.32. One batch with its queue attached — the payload of the review screen.</summary>
public sealed record ContentAdaptationJobDto(
    ContentAdaptationJobSummaryDto Summary,
    IReadOnlyList<ContentAdaptationItemSummaryDto> Items);
