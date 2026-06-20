namespace Sellevate.Social.Features.Discuss.Models;

public sealed record DiscussThreadQuery(
    string Sort,
    string? Search,
    string? Tag,
    int Page,
    int PageSize,
    bool IncludeAll);
