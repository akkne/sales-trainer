namespace Sellevate.Ai.Features.Companies.Models;

public sealed record GenerateBriefingRequestDto(
    string CompanyDescription,
    string? Goal,
    List<BriefingCallLogDto> RecentCalls,
    List<string> FeedbackSummaries);
