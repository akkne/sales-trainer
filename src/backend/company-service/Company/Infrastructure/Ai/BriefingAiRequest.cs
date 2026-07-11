namespace Sellevate.Company.Infrastructure.Ai;

public sealed record BriefingCallLogItem(
    string? ContactName,
    string Subject,
    string Outcome,
    DateTime OccurredAt);

public sealed record BriefingAiRequest(
    string CompanyDescription,
    string? Goal,
    List<BriefingCallLogItem> RecentCalls,
    List<string> FeedbackSummaries);
