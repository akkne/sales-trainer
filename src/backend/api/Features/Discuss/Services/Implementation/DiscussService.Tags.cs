using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Services.Implementation;

public sealed partial class DiscussService
{
    // ===================== Tags & stats =====================

    public async Task<List<DiscussTagDto>> GetTagsAsync(bool curatedOnly, CancellationToken ct = default)
    {
        IQueryable<DiscussTag> query = _db.DiscussTags;
        if (curatedOnly) query = query.Where(tag => tag.IsCurated);

        return await query
            .OrderBy(tag => tag.Name)
            .Select(tag => new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated))
            .ToListAsync(ct);
    }

    public async Task<List<PopularTagDto>> GetPopularTagsAsync(int limit, CancellationToken ct = default)
    {
        if (limit < 1) limit = 10;

        return await _db.DiscussTags
            .Select(tag => new PopularTagDto(tag.Slug, tag.Name, tag.ThreadTags.Count))
            .Where(dto => dto.ThreadCount > 0)
            .OrderByDescending(dto => dto.ThreadCount)
            .ThenBy(dto => dto.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<DiscussStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var totalThreads = await _db.DiscussThreads.CountAsync(ct);
        var totalReplies = await _db.DiscussReplies.CountAsync(ct);

        var since = DateTime.UtcNow.AddDays(-7);

        var threadAuthorVotes = await (
            from vote in _db.DiscussVotes
            where vote.TargetType == DiscussVoteTarget.Thread && vote.CreatedAt >= since
            join thread in _db.DiscussThreads on vote.TargetId equals thread.Id
            group thread by thread.AuthorId into grouped
            select new { AuthorId = grouped.Key, Count = grouped.Count() }
        ).ToListAsync(ct);

        var replyAuthorVotes = await (
            from vote in _db.DiscussVotes
            where vote.TargetType == DiscussVoteTarget.Reply && vote.CreatedAt >= since
            join reply in _db.DiscussReplies on vote.TargetId equals reply.Id
            group reply by reply.AuthorId into grouped
            select new { AuthorId = grouped.Key, Count = grouped.Count() }
        ).ToListAsync(ct);

        var totals = new Dictionary<Guid, int>();
        foreach (var row in threadAuthorVotes)
            totals[row.AuthorId] = totals.GetValueOrDefault(row.AuthorId) + row.Count;
        foreach (var row in replyAuthorVotes)
            totals[row.AuthorId] = totals.GetValueOrDefault(row.AuthorId) + row.Count;

        var top = totals
            .OrderByDescending(kv => kv.Value)
            .Take(TopAuthorsCount)
            .ToList();

        var authorNames = await ResolveAuthorNamesAsync(top.Select(kv => kv.Key), ct);
        var topAuthors = top
            .Select(kv => new TopAuthorDto(kv.Key, authorNames.GetValueOrDefault(kv.Key, ""), AvatarUrls.For(kv.Key), kv.Value))
            .ToList();

        return new DiscussStatsDto(totalThreads, totalReplies, topAuthors);
    }

    // ===================== Admin =====================

    public async Task<bool> DeleteThreadAsync(Guid threadId, CancellationToken ct = default)
    {
        var thread = await _db.DiscussThreads
            .Include(t => t.Replies)
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);
        if (thread == null) return false;

        // Votes are polymorphic (no FK), so clean them up explicitly.
        var replyIds = thread.Replies.Select(r => r.Id).ToList();
        var votes = await _db.DiscussVotes
            .Where(v => (v.TargetType == DiscussVoteTarget.Thread && v.TargetId == threadId)
                || (v.TargetType == DiscussVoteTarget.Reply && replyIds.Contains(v.TargetId)))
            .ToListAsync(ct);
        _db.DiscussVotes.RemoveRange(votes);

        _db.DiscussThreads.Remove(thread); // cascades replies + thread-tags
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteReplyAsync(Guid replyId, CancellationToken ct = default)
    {
        var reply = await _db.DiscussReplies.FirstOrDefaultAsync(r => r.Id == replyId, ct);
        if (reply == null) return false;

        var thread = await _db.DiscussThreads.FirstOrDefaultAsync(t => t.Id == reply.ThreadId, ct);
        if (thread != null)
        {
            if (thread.AcceptedReplyId == replyId) thread.AcceptedReplyId = null;
            thread.ReplyCount = Math.Max(0, thread.ReplyCount - 1);
            thread.UpdatedAt = DateTime.UtcNow;
        }

        var votes = await _db.DiscussVotes
            .Where(v => v.TargetType == DiscussVoteTarget.Reply && v.TargetId == replyId)
            .ToListAsync(ct);
        _db.DiscussVotes.RemoveRange(votes);

        _db.DiscussReplies.Remove(reply);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DiscussThreadSummaryDto?> SetThreadFlagsAsync(
        Guid threadId, bool? isPinned, bool? isHot, CancellationToken ct = default)
    {
        var thread = await _db.DiscussThreads
            .Include(t => t.ThreadTags).ThenInclude(tt => tt.Tag)
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);
        if (thread == null) return null;

        if (isPinned.HasValue) thread.IsPinned = isPinned.Value;
        if (isHot.HasValue) thread.IsHot = isHot.Value;
        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var authorNames = await ResolveAuthorNamesAsync([thread.AuthorId], ct);
        return ToSummary(thread, authorNames, viewerHasUpvoted: false);
    }

    public async Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> CreateCuratedTagAsync(
        string name, string? slug, CancellationToken ct = default)
    {
        var finalSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug!);
        if (finalSlug.Length == 0) return (DiscussOperationStatus.Conflict, null);

        if (await _db.DiscussTags.AnyAsync(tag => tag.Slug == finalSlug, ct))
            return (DiscussOperationStatus.Conflict, null);

        var tag = new DiscussTag
        {
            Id = Guid.NewGuid(),
            Slug = finalSlug,
            Name = name.Trim(),
            IsCurated = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.DiscussTags.Add(tag);
        await _db.SaveChangesAsync(ct);

        return (DiscussOperationStatus.Success, new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated));
    }

    public async Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> UpdateTagAsync(
        Guid tagId, string? name, string? slug, CancellationToken ct = default)
    {
        var tag = await _db.DiscussTags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
        if (tag == null) return (DiscussOperationStatus.NotFound, null);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var finalSlug = Slugify(slug!);
            if (finalSlug.Length == 0) return (DiscussOperationStatus.Conflict, null);
            if (finalSlug != tag.Slug && await _db.DiscussTags.AnyAsync(t => t.Slug == finalSlug, ct))
                return (DiscussOperationStatus.Conflict, null);
            tag.Slug = finalSlug;
        }

        if (!string.IsNullOrWhiteSpace(name)) tag.Name = name!.Trim();

        await _db.SaveChangesAsync(ct);
        return (DiscussOperationStatus.Success, new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated));
    }

    public async Task<bool> DeleteTagAsync(Guid tagId, CancellationToken ct = default)
    {
        var tag = await _db.DiscussTags.FirstOrDefaultAsync(t => t.Id == tagId, ct);
        if (tag == null) return false;

        _db.DiscussTags.Remove(tag); // cascades thread-tags
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ===================== Helpers =====================

    private async Task<Dictionary<Guid, string>> ResolveAuthorNamesAsync(
        IEnumerable<Guid> authorIds, CancellationToken ct)
    {
        var ids = authorIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _db.Users
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, ct);
    }

    private async Task<HashSet<Guid>> GetUpvotedTargetIdsAsync(
        Guid userId, DiscussVoteTarget targetType, IReadOnlyList<Guid> targetIds, CancellationToken ct)
    {
        if (userId == Guid.Empty || targetIds.Count == 0) return [];

        var ids = await _db.DiscussVotes
            .Where(v => v.UserId == userId && v.TargetType == targetType && targetIds.Contains(v.TargetId))
            .Select(v => v.TargetId)
            .ToListAsync(ct);
        return [.. ids];
    }

    private static DiscussThreadSummaryDto ToSummary(
        DiscussThread thread, IReadOnlyDictionary<Guid, string> authorNames, bool viewerHasUpvoted) =>
        new(
            thread.Id,
            thread.Title,
            Preview(thread.Body),
            thread.AuthorId,
            authorNames.GetValueOrDefault(thread.AuthorId, ""),
            AvatarUrls.For(thread.AuthorId),
            thread.UpvoteCount,
            thread.ReplyCount,
            thread.ViewCount,
            thread.IsPinned,
            thread.IsHot,
            thread.AcceptedReplyId != null,
            thread.ThreadTags.Select(tt => new TagRefDto(tt.Tag.Slug, tt.Tag.Name)).ToList(),
            thread.CreatedAt,
            thread.LastActivityAt,
            viewerHasUpvoted);

    private static DiscussThreadDetailDto ToDetail(
        DiscussThread thread, IReadOnlyDictionary<Guid, string> authorNames,
        bool viewerHasUpvoted, IReadOnlyList<DiscussReplyDto> replies) =>
        new(
            thread.Id,
            thread.Title,
            thread.Body,
            thread.AuthorId,
            authorNames.GetValueOrDefault(thread.AuthorId, ""),
            AvatarUrls.For(thread.AuthorId),
            thread.UpvoteCount,
            thread.ReplyCount,
            thread.ViewCount,
            thread.IsPinned,
            thread.IsHot,
            thread.AcceptedReplyId != null,
            thread.AcceptedReplyId,
            thread.ThreadTags.Select(tt => new TagRefDto(tt.Tag.Slug, tt.Tag.Name)).ToList(),
            thread.CreatedAt,
            thread.LastActivityAt,
            viewerHasUpvoted,
            replies);

    private static DiscussReplyDto ToReplyDto(
        DiscussReply reply, IReadOnlyDictionary<Guid, string> authorNames, bool viewerHasUpvoted) =>
        new(
            reply.Id,
            reply.ThreadId,
            reply.AuthorId,
            authorNames.GetValueOrDefault(reply.AuthorId, ""),
            AvatarUrls.For(reply.AuthorId),
            reply.Body,
            reply.UpvoteCount,
            reply.IsAccepted,
            reply.CreatedAt,
            viewerHasUpvoted);

    private static string Preview(string body) =>
        body.Length <= BodyPreviewLength ? body : body[..BodyPreviewLength].TrimEnd() + "…";

    private static string Slugify(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = DashCollapseRegex().Replace(slug, "-").Trim('-');
        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex DashCollapseRegex();
}
