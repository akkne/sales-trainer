using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Abstract;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Infrastructure.Storage.Abstract;

namespace Sellevate.Social.Features.Discuss.Services.Implementation;

internal sealed partial class DiscussService : IDiscussService
{
    private const int BodyPreviewLength = 240;
    private const int HotCandidateCap = 1000;
    private const int TopAuthorsCount = 3;

    private readonly SocialDbContext _databaseContext;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<DiscussService> _logger;

    public DiscussService(SocialDbContext databaseContext, IObjectStorage objectStorage, ILogger<DiscussService> logger)
    {
        _databaseContext = databaseContext;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task<PagedResultDto<DiscussThreadSummaryDto>> ListThreadsAsync(
        DiscussThreadQuery query, Guid viewerId, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        IQueryable<DiscussThread> filtered = _databaseContext.DiscussThreads
            .Include(thread => thread.ThreadTags)
            .ThenInclude(threadTag => threadTag.Tag);

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tagSlug = query.Tag.Trim().ToLowerInvariant();
            filtered = filtered.Where(thread => thread.ThreadTags.Any(threadTag => threadTag.Tag.Slug == tagSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var escapedSearch = query.Search.Trim()
                .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escapedSearch}%";
            filtered = filtered.Where(thread =>
                EF.Functions.ILike(thread.Title, pattern, "\\") ||
                EF.Functions.ILike(thread.Body, pattern, "\\"));
        }

        var sort = (query.Sort ?? "hot").ToLowerInvariant();
        if (sort == "unanswered")
        {
            filtered = filtered.Where(thread => thread.ReplyCount == 0);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        List<DiscussThread> pageItems;
        if (sort == "new")
        {
            pageItems = await filtered
                .OrderByDescending(thread => thread.LastActivityAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        else if (sort == "unanswered")
        {
            pageItems = await filtered
                .OrderByDescending(thread => thread.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var candidates = await filtered
                .OrderByDescending(thread => thread.IsPinned)
                .ThenByDescending(thread => thread.UpvoteCount)
                .ThenByDescending(thread => thread.LastActivityAt)
                .Take(HotCandidateCap)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            pageItems = candidates
                .OrderByDescending(thread => thread.IsPinned)
                .ThenByDescending(thread => HotScore(thread, now))
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToList();
        }

        var authorNames = await ResolveAuthorNamesAsync(pageItems.Select(thread => thread.AuthorId), cancellationToken);
        var upvotedThreadIds = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Thread, pageItems.Select(thread => thread.Id).ToList(), cancellationToken);
        var photosByThreadId = await LoadThreadPhotosByThreadIdAsync(pageItems.Select(thread => thread.Id).ToList(), cancellationToken);

        var items = pageItems
            .Select(thread =>
            {
                var threadPhotos = photosByThreadId.GetValueOrDefault(thread.Id, Array.Empty<DiscussPhoto>());
                var firstPhotoUrl = threadPhotos.Count == 0
                    ? null
                    : Services.DiscussPhotoUrlBuilder.Build(threadPhotos.MinBy(photo => photo.OrderIndex)!.Id);
                return ToSummary(thread, authorNames, upvotedThreadIds.Contains(thread.Id), threadPhotos.Count, firstPhotoUrl);
            })
            .ToList();

        return new PagedResultDto<DiscussThreadSummaryDto>(items, page, pageSize, totalCount);
    }

    private static double HotScore(DiscussThread thread, DateTime now)
    {
        var hours = Math.Max(0, (now - thread.LastActivityAt).TotalHours);
        var baseScore = thread.UpvoteCount * 4 + thread.ReplyCount * 2 + Math.Log10(thread.ViewCount + 1);
        var decayed = baseScore / Math.Pow(hours + 2, 0.5);
        if (thread.IsHot) decayed += 1000;
        return decayed;
    }

    public async Task<DiscussThreadDetailDto?> GetThreadAsync(
        Guid threadId, Guid viewerId, bool incrementView, CancellationToken cancellationToken = default)
    {
        var thread = await _databaseContext.DiscussThreads
            .Include(candidate => candidate.ThreadTags).ThenInclude(threadTag => threadTag.Tag)
            .Include(candidate => candidate.Replies)
            .FirstOrDefaultAsync(candidate => candidate.Id == threadId, cancellationToken);

        if (thread == null) return null;

        if (incrementView)
        {
            thread.ViewCount += 1;
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        var replyIds = thread.Replies.Select(reply => reply.Id).ToList();
        var authorIds = thread.Replies.Select(reply => reply.AuthorId).Append(thread.AuthorId);
        var authorNames = await ResolveAuthorNamesAsync(authorIds, cancellationToken);

        var threadUpvoted = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Thread, [thread.Id], cancellationToken);
        var upvotedReplyIds = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Reply, replyIds, cancellationToken);

        var (threadPhotos, replyPhotosByReplyId) = await LoadThreadAndReplyPhotosAsync(thread.Id, replyIds, cancellationToken);

        var replies = thread.Replies
            .OrderBy(reply => reply.CreatedAt)
            .Select(reply => ToReplyDto(
                reply,
                authorNames,
                upvotedReplyIds.Contains(reply.Id),
                replyPhotosByReplyId.GetValueOrDefault(reply.Id, Array.Empty<DiscussPhotoDto>())))
            .ToList();

        return ToDetail(thread, authorNames, threadUpvoted.Contains(thread.Id), replies, threadPhotos);
    }

    public async Task<DiscussThreadDetailDto> CreateThreadAsync(
        Guid authorId, string title, string body, IReadOnlyList<string> tagLabels, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var thread = new DiscussThread
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Title = title.Trim(),
            Body = body.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            LastActivityAt = now
        };

        var tags = await ResolveOrCreateTagsAsync(tagLabels, cancellationToken);
        foreach (var tag in tags)
        {
            thread.ThreadTags.Add(new DiscussThreadTag
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                TagId = tag.Id,
                Tag = tag
            });
        }

        _databaseContext.DiscussThreads.Add(thread);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var authorNames = await ResolveAuthorNamesAsync([authorId], cancellationToken);
        return ToDetail(thread, authorNames, viewerHasUpvoted: false, replies: [], photos: Array.Empty<DiscussPhotoDto>());
    }

    private async Task<List<DiscussTag>> ResolveOrCreateTagsAsync(
        IReadOnlyList<string> tagLabels, CancellationToken cancellationToken)
    {
        var bySlug = new Dictionary<string, (string Label, string Slug)>();
        foreach (var label in tagLabels)
        {
            if (string.IsNullOrWhiteSpace(label)) continue;
            var slug = Slugify(label);
            if (slug.Length == 0) continue;
            bySlug[slug] = (label.Trim(), slug);
        }

        if (bySlug.Count == 0) return [];

        var slugs = bySlug.Keys.ToList();
        var existing = await _databaseContext.DiscussTags.Where(tag => slugs.Contains(tag.Slug)).ToListAsync(cancellationToken);
        var result = new List<DiscussTag>(existing);

        foreach (var slug in slugs)
        {
            if (existing.Any(tag => tag.Slug == slug)) continue;
            var created = new DiscussTag
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = bySlug[slug].Label,
                IsCurated = false,
                CreatedAt = DateTime.UtcNow
            };
            _databaseContext.DiscussTags.Add(created);
            result.Add(created);
        }

        return result;
    }

    public async Task<DiscussReplyDto?> AddReplyAsync(
        Guid threadId, Guid authorId, string body, CancellationToken cancellationToken = default)
    {
        var thread = await _databaseContext.DiscussThreads.FirstOrDefaultAsync(candidate => candidate.Id == threadId, cancellationToken);
        if (thread == null) return null;

        var now = DateTime.UtcNow;
        var reply = new DiscussReply
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            AuthorId = authorId,
            Body = body.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _databaseContext.DiscussReplies.Add(reply);

        thread.ReplyCount += 1;
        thread.LastActivityAt = now;
        thread.UpdatedAt = now;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        var authorNames = await ResolveAuthorNamesAsync([authorId], cancellationToken);
        return ToReplyDto(reply, authorNames, viewerHasUpvoted: false, photos: Array.Empty<DiscussPhotoDto>());
    }

    public Task<VoteResultDto?> SetThreadVoteAsync(Guid threadId, Guid userId, bool upvote, CancellationToken cancellationToken = default)
        => SetVoteAsync(DiscussVoteTarget.Thread, threadId, userId, upvote, cancellationToken);

    public Task<VoteResultDto?> SetReplyVoteAsync(Guid replyId, Guid userId, bool upvote, CancellationToken cancellationToken = default)
        => SetVoteAsync(DiscussVoteTarget.Reply, replyId, userId, upvote, cancellationToken);

    private async Task<VoteResultDto?> SetVoteAsync(
        DiscussVoteTarget targetType, Guid targetId, Guid userId, bool upvote, CancellationToken cancellationToken)
    {
        DiscussThread? thread = null;
        DiscussReply? reply = null;
        if (targetType == DiscussVoteTarget.Thread)
        {
            thread = await _databaseContext.DiscussThreads.FirstOrDefaultAsync(candidate => candidate.Id == targetId, cancellationToken);
            if (thread == null) return null;
        }
        else
        {
            reply = await _databaseContext.DiscussReplies.FirstOrDefaultAsync(candidate => candidate.Id == targetId, cancellationToken);
            if (reply == null) return null;
        }

        var existing = await _databaseContext.DiscussVotes.FirstOrDefaultAsync(
            vote => vote.UserId == userId && vote.TargetType == targetType && vote.TargetId == targetId, cancellationToken);

        if (upvote && existing == null)
        {
            _databaseContext.DiscussVotes.Add(new DiscussVote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TargetType = targetType,
                TargetId = targetId,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (!upvote && existing != null)
        {
            _databaseContext.DiscussVotes.Remove(existing);
        }

        // Recompute the vote count from the DiscussVotes table within the same transaction
        // to avoid read-then-write drift under concurrent votes.
        // The pending Add/Remove above is reflected by SaveChangesAsync below.
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var count = await _databaseContext.DiscussVotes
            .CountAsync(v => v.TargetType == targetType && v.TargetId == targetId, cancellationToken);

        // Keep the denormalised counter in sync with the authoritative vote count.
        if (thread != null)
        {
            thread.UpvoteCount = count;
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else if (reply != null)
        {
            reply.UpvoteCount = count;
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new VoteResultDto(count, upvote);
    }

    public async Task<(DiscussOperationStatus Status, DiscussThreadDetailDto? Thread)> SetAcceptedReplyAsync(
        Guid threadId, Guid actingUserId, bool isAdmin, Guid? replyId, CancellationToken cancellationToken = default)
    {
        var thread = await _databaseContext.DiscussThreads
            .Include(candidate => candidate.Replies)
            .FirstOrDefaultAsync(candidate => candidate.Id == threadId, cancellationToken);

        if (thread == null) return (DiscussOperationStatus.NotFound, null);
        if (thread.AuthorId != actingUserId && !isAdmin) return (DiscussOperationStatus.Forbidden, null);

        foreach (var existing in thread.Replies.Where(reply => reply.IsAccepted))
            existing.IsAccepted = false;

        if (replyId is { } identifier && identifier != Guid.Empty)
        {
            var reply = thread.Replies.FirstOrDefault(candidate => candidate.Id == identifier);
            if (reply == null) return (DiscussOperationStatus.NotFound, null);
            reply.IsAccepted = true;
            thread.AcceptedReplyId = reply.Id;
        }
        else
        {
            thread.AcceptedReplyId = null;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var detail = await GetThreadAsync(threadId, actingUserId, incrementView: false, cancellationToken);
        return (DiscussOperationStatus.Success, detail);
    }
}
