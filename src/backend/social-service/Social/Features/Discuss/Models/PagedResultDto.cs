namespace Sellevate.Social.Features.Discuss.Models;

public sealed record PagedResultDto<TItem>(IReadOnlyList<TItem> Items, int Page, int PageSize, int TotalCount);
