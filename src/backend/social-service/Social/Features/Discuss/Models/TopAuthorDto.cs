namespace Sellevate.Social.Features.Discuss.Models;

public sealed record TopAuthorDto(Guid AuthorId, string AuthorName, string AuthorAvatarUrl, int UpvotesReceived);
