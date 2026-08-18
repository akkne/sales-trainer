namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One batch as the admin list shows it: what it covers, where it is, and how much of
/// it is still waiting for a person.
/// </summary>
/// <param name="PendingCount">Items the model has not answered yet — the part that still costs money.</param>
/// <param name="AwaitingReviewCount">
/// Items waiting for a person. <b>The number the screen is really about</b>: a batch is not done
/// when the model finishes, it is done when somebody has said yes or no to every proposal.
/// </param>
public sealed record ContentAdaptationJobSummaryDto(
    Guid Id,
    string Mode,
    string StageKey,
    string Status,
    int ItemCount,
    int PendingCount,
    int AwaitingReviewCount,
    int AcceptedCount,
    int RejectedCount,
    int UnchangedCount,
    int FailedCount,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt);
