using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;
using SalesTrainer.Api.Features.Discuss.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Discuss.Services.Implementation;

public sealed partial class DiscussService : IDiscussService
{
    private const int BodyPreviewLength = 240;
    private const int HotCandidateCap = 1000;
    private const int TopAuthorsCount = 3;

    private readonly AppDbContext _db;

    public DiscussService(AppDbContext db) => _db = db;

    // ===================== Listing =====================

    public async Task<PagedResultDto<DiscussThreadSummaryDto>> ListThreadsAsync(
        DiscussThreadQuery query, Guid viewerId, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        IQueryable<DiscussThread> filtered = _db.DiscussThreads
            .Include(thread => thread.ThreadTags)
            .ThenInclude(threadTag => threadTag.Tag);

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tagSlug = query.Tag.Trim().ToLowerInvariant();
            filtered = filtered.Where(thread => thread.ThreadTags.Any(tt => tt.Tag.Slug == tagSlug));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            filtered = filtered.Where(thread =>
                EF.Functions.ILike(thread.Title, pattern) || EF.Functions.ILike(thread.Body, pattern));
        }

        var sort = (query.Sort ?? "hot").ToLowerInvariant();
        if (sort == "unanswered")
        {
            filtered = filtered.Where(thread => thread.ReplyCount == 0);
        }

        var totalCount = await filtered.CountAsync(ct);

        List<DiscussThread> pageItems;
        if (sort == "new")
        {
            pageItems = await filtered
                .OrderByDescending(thread => thread.LastActivityAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
        }
        else if (sort == "unanswered")
        {
            pageItems = await filtered
                .OrderByDescending(thread => thread.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
        }
        else // "hot" (default): pinned first, then time-decayed score computed in memory
        {
            var candidates = await filtered
                .OrderByDescending(thread => thread.IsPinned)
                .ThenByDescending(thread => thread.UpvoteCount)
                .ThenByDescending(thread => thread.LastActivityAt)
                .Take(HotCandidateCap)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            pageItems = candidates
                .OrderByDescending(thread => thread.IsPinned)
                .ThenByDescending(thread => HotScore(thread, now))
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToList();
        }

        var authorNames = await ResolveAuthorNamesAsync(pageItems.Select(t => t.AuthorId), ct);
        var upvotedThreadIds = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Thread, pageItems.Select(t => t.Id).ToList(), ct);

        var items = pageItems
            .Select(thread => ToSummary(thread, authorNames, upvotedThreadIds.Contains(thread.Id), photoCount: 0, firstPhotoUrl: null))
            .ToList();

        return new PagedResultDto<DiscussThreadSummaryDto>(items, page, pageSize, totalCount);
    }

    /// <summary>Time-decayed hot score; a manual "hot" flag adds a large boost.</summary>
    private static double HotScore(DiscussThread thread, DateTime now)
    {
        var hours = Math.Max(0, (now - thread.LastActivityAt).TotalHours);
        var baseScore = thread.UpvoteCount * 4 + thread.ReplyCount * 2 + Math.Log10(thread.ViewCount + 1);
        var decayed = baseScore / Math.Pow(hours + 2, 0.5);
        if (thread.IsHot) decayed += 1000;
        return decayed;
    }

    // ===================== Get one =====================

    public async Task<DiscussThreadDetailDto?> GetThreadAsync(
        Guid threadId, Guid viewerId, bool incrementView, CancellationToken ct = default)
    {
        var thread = await _db.DiscussThreads
            .Include(t => t.ThreadTags).ThenInclude(tt => tt.Tag)
            .Include(t => t.Replies)
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);

        if (thread == null) return null;

        if (incrementView)
        {
            thread.ViewCount += 1;
            await _db.SaveChangesAsync(ct);
        }

        var replyIds = thread.Replies.Select(r => r.Id).ToList();
        var authorIds = thread.Replies.Select(r => r.AuthorId).Append(thread.AuthorId);
        var authorNames = await ResolveAuthorNamesAsync(authorIds, ct);

        var threadUpvoted = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Thread, [thread.Id], ct);
        var upvotedReplyIds = await GetUpvotedTargetIdsAsync(
            viewerId, DiscussVoteTarget.Reply, replyIds, ct);

        var replies = thread.Replies
            .OrderBy(r => r.CreatedAt)
            .Select(r => ToReplyDto(r, authorNames, upvotedReplyIds.Contains(r.Id), Array.Empty<DiscussPhotoDto>()))
            .ToList();

        return ToDetail(thread, authorNames, threadUpvoted.Contains(thread.Id), replies, Array.Empty<DiscussPhotoDto>());
    }

    // ===================== Create thread =====================

    public async Task<DiscussThreadDetailDto> CreateThreadAsync(
        Guid authorId, string title, string body, IReadOnlyList<string> tagLabels, CancellationToken ct = default)
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

        var tags = await ResolveOrCreateTagsAsync(tagLabels, ct);
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

        _db.DiscussThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        var authorNames = await ResolveAuthorNamesAsync([authorId], ct);
        return ToDetail(thread, authorNames, viewerHasUpvoted: false, replies: [], photos: Array.Empty<DiscussPhotoDto>());
    }

    /// <summary>Resolves existing tags by slug (curated or not) and creates free-form ones as needed.</summary>
    private async Task<List<DiscussTag>> ResolveOrCreateTagsAsync(
        IReadOnlyList<string> tagLabels, CancellationToken ct)
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
        var existing = await _db.DiscussTags.Where(tag => slugs.Contains(tag.Slug)).ToListAsync(ct);
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
            _db.DiscussTags.Add(created);
            result.Add(created);
        }

        return result;
    }

    // ===================== Replies =====================

    public async Task<DiscussReplyDto?> AddReplyAsync(
        Guid threadId, Guid authorId, string body, CancellationToken ct = default)
    {
        var thread = await _db.DiscussThreads.FirstOrDefaultAsync(t => t.Id == threadId, ct);
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
        _db.DiscussReplies.Add(reply);

        thread.ReplyCount += 1;
        thread.LastActivityAt = now;
        thread.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        var authorNames = await ResolveAuthorNamesAsync([authorId], ct);
        return ToReplyDto(reply, authorNames, viewerHasUpvoted: false, photos: Array.Empty<DiscussPhotoDto>());
    }

    // ===================== Voting =====================

    public Task<VoteResultDto?> SetThreadVoteAsync(Guid threadId, Guid userId, bool upvote, CancellationToken ct = default)
        => SetVoteAsync(DiscussVoteTarget.Thread, threadId, userId, upvote, ct);

    public Task<VoteResultDto?> SetReplyVoteAsync(Guid replyId, Guid userId, bool upvote, CancellationToken ct = default)
        => SetVoteAsync(DiscussVoteTarget.Reply, replyId, userId, upvote, ct);

    private async Task<VoteResultDto?> SetVoteAsync(
        DiscussVoteTarget targetType, Guid targetId, Guid userId, bool upvote, CancellationToken ct)
    {
        // Load the target so we can keep its denormalized counter in sync.
        DiscussThread? thread = null;
        DiscussReply? reply = null;
        if (targetType == DiscussVoteTarget.Thread)
        {
            thread = await _db.DiscussThreads.FirstOrDefaultAsync(t => t.Id == targetId, ct);
            if (thread == null) return null;
        }
        else
        {
            reply = await _db.DiscussReplies.FirstOrDefaultAsync(r => r.Id == targetId, ct);
            if (reply == null) return null;
        }

        var existing = await _db.DiscussVotes.FirstOrDefaultAsync(
            v => v.UserId == userId && v.TargetType == targetType && v.TargetId == targetId, ct);

        if (upvote && existing == null)
        {
            _db.DiscussVotes.Add(new DiscussVote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TargetType = targetType,
                TargetId = targetId,
                CreatedAt = DateTime.UtcNow
            });
            if (thread != null) thread.UpvoteCount += 1; else reply!.UpvoteCount += 1;
            await _db.SaveChangesAsync(ct);
        }
        else if (!upvote && existing != null)
        {
            _db.DiscussVotes.Remove(existing);
            if (thread != null) thread.UpvoteCount = Math.Max(0, thread.UpvoteCount - 1);
            else reply!.UpvoteCount = Math.Max(0, reply.UpvoteCount - 1);
            await _db.SaveChangesAsync(ct);
        }

        var count = thread?.UpvoteCount ?? reply!.UpvoteCount;
        return new VoteResultDto(count, upvote);
    }

    // ===================== Accepted reply =====================

    public async Task<(DiscussOperationStatus Status, DiscussThreadDetailDto? Thread)> SetAcceptedReplyAsync(
        Guid threadId, Guid actingUserId, bool isAdmin, Guid? replyId, CancellationToken ct = default)
    {
        var thread = await _db.DiscussThreads
            .Include(t => t.Replies)
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);

        if (thread == null) return (DiscussOperationStatus.NotFound, null);
        if (thread.AuthorId != actingUserId && !isAdmin) return (DiscussOperationStatus.Forbidden, null);

        // Clear the previously accepted reply, if any.
        foreach (var existing in thread.Replies.Where(r => r.IsAccepted))
            existing.IsAccepted = false;

        if (replyId is { } id && id != Guid.Empty)
        {
            var reply = thread.Replies.FirstOrDefault(r => r.Id == id);
            if (reply == null) return (DiscussOperationStatus.NotFound, null);
            reply.IsAccepted = true;
            thread.AcceptedReplyId = reply.Id;
        }
        else
        {
            thread.AcceptedReplyId = null;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var detail = await GetThreadAsync(threadId, actingUserId, incrementView: false, ct);
        return (DiscussOperationStatus.Success, detail);
    }
}
