using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Services.Implementation;

internal sealed partial class DiscussService
{
    public async Task<List<DiscussTagDto>> GetTagsAsync(bool curatedOnly, CancellationToken cancellationToken = default)
    {
        IQueryable<DiscussTag> query = _databaseContext.DiscussTags;
        if (curatedOnly) query = query.Where(tag => tag.IsCurated);

        return await query
            .OrderBy(tag => tag.Name)
            .Select(tag => new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PopularTagDto>> GetPopularTagsAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 10;

        return await _databaseContext.DiscussTags
            .Select(tag => new PopularTagDto(tag.Slug, tag.Name, tag.ThreadTags.Count))
            .Where(dto => dto.ThreadCount > 0)
            .OrderByDescending(dto => dto.ThreadCount)
            .ThenBy(dto => dto.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<DiscussStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalThreads = await _databaseContext.DiscussThreads.CountAsync(cancellationToken);
        var totalReplies = await _databaseContext.DiscussReplies.CountAsync(cancellationToken);

        var since = DateTime.UtcNow.AddDays(-7);

        var threadAuthorVotes = await (
            from vote in _databaseContext.DiscussVotes
            where vote.TargetType == DiscussVoteTarget.Thread && vote.CreatedAt >= since
            join thread in _databaseContext.DiscussThreads on vote.TargetId equals thread.Id
            group thread by thread.AuthorId into grouped
            select new { AuthorId = grouped.Key, Count = grouped.Count() }
        ).ToListAsync(cancellationToken);

        var replyAuthorVotes = await (
            from vote in _databaseContext.DiscussVotes
            where vote.TargetType == DiscussVoteTarget.Reply && vote.CreatedAt >= since
            join reply in _databaseContext.DiscussReplies on vote.TargetId equals reply.Id
            group reply by reply.AuthorId into grouped
            select new { AuthorId = grouped.Key, Count = grouped.Count() }
        ).ToListAsync(cancellationToken);

        var totals = new Dictionary<Guid, int>();
        foreach (var row in threadAuthorVotes)
            totals[row.AuthorId] = totals.GetValueOrDefault(row.AuthorId) + row.Count;
        foreach (var row in replyAuthorVotes)
            totals[row.AuthorId] = totals.GetValueOrDefault(row.AuthorId) + row.Count;

        var top = totals
            .OrderByDescending(entry => entry.Value)
            .Take(TopAuthorsCount)
            .ToList();

        var authorNames = await ResolveAuthorNamesAsync(top.Select(entry => entry.Key), cancellationToken);
        var topAuthors = top
            .Select(entry => new TopAuthorDto(entry.Key, authorNames.GetValueOrDefault(entry.Key, ""), AvatarUrls.For(entry.Key), entry.Value))
            .ToList();

        return new DiscussStatsDto(totalThreads, totalReplies, topAuthors);
    }

    public async Task<bool> DeleteThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var thread = await _databaseContext.DiscussThreads
            .Include(candidate => candidate.Replies)
            .FirstOrDefaultAsync(candidate => candidate.Id == threadId, cancellationToken);
        if (thread == null) return false;

        var replyIds = thread.Replies.Select(reply => reply.Id).ToList();
        var votes = await _databaseContext.DiscussVotes
            .Where(vote => (vote.TargetType == DiscussVoteTarget.Thread && vote.TargetId == threadId)
                || (vote.TargetType == DiscussVoteTarget.Reply && replyIds.Contains(vote.TargetId)))
            .ToListAsync(cancellationToken);
        _databaseContext.DiscussVotes.RemoveRange(votes);

        var photos = await _databaseContext.DiscussPhotos
            .Where(photo => (photo.OwnerType == DiscussPhotoOwner.Thread && photo.OwnerId == threadId)
                || (photo.OwnerType == DiscussPhotoOwner.Reply && replyIds.Contains(photo.OwnerId)))
            .ToListAsync(cancellationToken);
        var photoObjectKeys = photos.Select(photo => photo.ObjectKey).ToList();
        _databaseContext.DiscussPhotos.RemoveRange(photos);

        _databaseContext.DiscussThreads.Remove(thread);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        foreach (var objectKey in photoObjectKeys)
            await DeleteObjectBestEffortAsync(objectKey, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteReplyAsync(Guid replyId, CancellationToken cancellationToken = default)
    {
        var reply = await _databaseContext.DiscussReplies.FirstOrDefaultAsync(candidate => candidate.Id == replyId, cancellationToken);
        if (reply == null) return false;

        var thread = await _databaseContext.DiscussThreads.FirstOrDefaultAsync(candidate => candidate.Id == reply.ThreadId, cancellationToken);
        if (thread != null)
        {
            if (thread.AcceptedReplyId == replyId) thread.AcceptedReplyId = null;
            thread.ReplyCount = Math.Max(0, thread.ReplyCount - 1);
            thread.UpdatedAt = DateTime.UtcNow;
        }

        var votes = await _databaseContext.DiscussVotes
            .Where(vote => vote.TargetType == DiscussVoteTarget.Reply && vote.TargetId == replyId)
            .ToListAsync(cancellationToken);
        _databaseContext.DiscussVotes.RemoveRange(votes);

        var photos = await _databaseContext.DiscussPhotos
            .Where(photo => photo.OwnerType == DiscussPhotoOwner.Reply && photo.OwnerId == replyId)
            .ToListAsync(cancellationToken);
        var photoObjectKeys = photos.Select(photo => photo.ObjectKey).ToList();
        _databaseContext.DiscussPhotos.RemoveRange(photos);

        _databaseContext.DiscussReplies.Remove(reply);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        foreach (var objectKey in photoObjectKeys)
            await DeleteObjectBestEffortAsync(objectKey, cancellationToken);

        return true;
    }

    public async Task<DiscussThreadSummaryDto?> SetThreadFlagsAsync(
        Guid threadId, bool? isPinned, bool? isHot, CancellationToken cancellationToken = default)
    {
        var thread = await _databaseContext.DiscussThreads
            .Include(candidate => candidate.ThreadTags).ThenInclude(threadTag => threadTag.Tag)
            .FirstOrDefaultAsync(candidate => candidate.Id == threadId, cancellationToken);
        if (thread == null) return null;

        if (isPinned.HasValue) thread.IsPinned = isPinned.Value;
        if (isHot.HasValue) thread.IsHot = isHot.Value;
        thread.UpdatedAt = DateTime.UtcNow;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var authorNames = await ResolveAuthorNamesAsync([thread.AuthorId], cancellationToken);
        var photosByThreadId = await LoadThreadPhotosByThreadIdAsync([thread.Id], cancellationToken);
        var threadPhotos = photosByThreadId.GetValueOrDefault(thread.Id, Array.Empty<DiscussPhoto>());
        var firstPhotoUrl = threadPhotos.Count == 0
            ? null
            : Services.DiscussPhotoUrlBuilder.Build(threadPhotos.MinBy(photo => photo.OrderIndex)!.Id);
        return ToSummary(thread, authorNames, viewerHasUpvoted: false, threadPhotos.Count, firstPhotoUrl);
    }

    public async Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> CreateCuratedTagAsync(
        string name, string? slug, CancellationToken cancellationToken = default)
    {
        var finalSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug!);
        if (finalSlug.Length == 0) return (DiscussOperationStatus.Conflict, null);

        if (await _databaseContext.DiscussTags.AnyAsync(tag => tag.Slug == finalSlug, cancellationToken))
            return (DiscussOperationStatus.Conflict, null);

        var tag = new DiscussTag
        {
            Id = Guid.NewGuid(),
            Slug = finalSlug,
            Name = name.Trim(),
            IsCurated = true,
            CreatedAt = DateTime.UtcNow
        };
        _databaseContext.DiscussTags.Add(tag);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return (DiscussOperationStatus.Success, new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated));
    }

    public async Task<(DiscussOperationStatus Status, DiscussTagDto? Tag)> UpdateTagAsync(
        Guid tagId, string? name, string? slug, CancellationToken cancellationToken = default)
    {
        var tag = await _databaseContext.DiscussTags.FirstOrDefaultAsync(candidate => candidate.Id == tagId, cancellationToken);
        if (tag == null) return (DiscussOperationStatus.NotFound, null);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var finalSlug = Slugify(slug!);
            if (finalSlug.Length == 0) return (DiscussOperationStatus.Conflict, null);
            if (finalSlug != tag.Slug && await _databaseContext.DiscussTags.AnyAsync(candidate => candidate.Slug == finalSlug, cancellationToken))
                return (DiscussOperationStatus.Conflict, null);
            tag.Slug = finalSlug;
        }

        if (!string.IsNullOrWhiteSpace(name)) tag.Name = name!.Trim();

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return (DiscussOperationStatus.Success, new DiscussTagDto(tag.Id, tag.Slug, tag.Name, tag.IsCurated));
    }

    public async Task<bool> DeleteTagAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        var tag = await _databaseContext.DiscussTags.FirstOrDefaultAsync(candidate => candidate.Id == tagId, cancellationToken);
        if (tag == null) return false;

        _databaseContext.DiscussTags.Remove(tag);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Dictionary<Guid, string>> ResolveAuthorNamesAsync(
        IEnumerable<Guid> authorIds, CancellationToken cancellationToken)
    {
        var ids = authorIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        return await _databaseContext.UserReplicas
            .Where(replica => ids.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);
    }

    private async Task<HashSet<Guid>> GetUpvotedTargetIdsAsync(
        Guid userId, DiscussVoteTarget targetType, IReadOnlyList<Guid> targetIds, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || targetIds.Count == 0) return [];

        var ids = await _databaseContext.DiscussVotes
            .Where(vote => vote.UserId == userId && vote.TargetType == targetType && targetIds.Contains(vote.TargetId))
            .Select(vote => vote.TargetId)
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    private static DiscussThreadSummaryDto ToSummary(
        DiscussThread thread, IReadOnlyDictionary<Guid, string> authorNames, bool viewerHasUpvoted,
        int photoCount, string? firstPhotoUrl) =>
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
            thread.ThreadTags.Select(threadTag => new TagRefDto(threadTag.Tag.Slug, threadTag.Tag.Name)).ToList(),
            thread.CreatedAt,
            thread.LastActivityAt,
            viewerHasUpvoted,
            photoCount,
            firstPhotoUrl);

    private static DiscussThreadDetailDto ToDetail(
        DiscussThread thread, IReadOnlyDictionary<Guid, string> authorNames,
        bool viewerHasUpvoted, IReadOnlyList<DiscussReplyDto> replies,
        IReadOnlyList<DiscussPhotoDto> photos) =>
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
            thread.ThreadTags.Select(threadTag => new TagRefDto(threadTag.Tag.Slug, threadTag.Tag.Name)).ToList(),
            thread.CreatedAt,
            thread.LastActivityAt,
            viewerHasUpvoted,
            replies,
            photos);

    private static DiscussReplyDto ToReplyDto(
        DiscussReply reply, IReadOnlyDictionary<Guid, string> authorNames, bool viewerHasUpvoted,
        IReadOnlyList<DiscussPhotoDto> photos) =>
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
            viewerHasUpvoted,
            photos);

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
