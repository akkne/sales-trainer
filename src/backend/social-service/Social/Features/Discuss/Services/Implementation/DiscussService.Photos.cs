using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Features.Discuss.Services.Implementation;

internal sealed partial class DiscussService
{
    /// <summary>
    /// Stores a batch of photos against a thread or a reply, but only for the person who authored it.
    /// The batch is all-or-nothing: every file is validated before any object is written, and a
    /// failed database commit deletes whatever already reached object storage — otherwise a rolled-back
    /// upload would leave orphaned objects nothing can ever reference or bill for.
    ///
    /// <para>
    /// The per-owner cap counts photos already stored, so it bounds the total rather than one request.
    /// </para>
    /// </summary>
    public async Task<(DiscussPhotoUploadStatus Status, IReadOnlyList<DiscussPhotoDto> Photos)> UploadPhotosAsync(
        DiscussPhotoOwner ownerType,
        Guid ownerId,
        Guid actingUserId,
        IReadOnlyList<DiscussPhotoUploadFile> files,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(_databaseContext, cancellationToken);

        var ownerAuthorId = await ResolveOwnerAuthorIdAsync(ownerType, ownerId, cancellationToken);
        if (ownerAuthorId is null)
            return (DiscussPhotoUploadStatus.OwnerNotFound, Array.Empty<DiscussPhotoDto>());

        if (ownerAuthorId.Value != actingUserId)
            return (DiscussPhotoUploadStatus.Forbidden, Array.Empty<DiscussPhotoDto>());

        var existingCount = await _databaseContext.DiscussPhotos
            .CountAsync(photo => photo.OwnerType == ownerType && photo.OwnerId == ownerId, cancellationToken);

        if (existingCount + files.Count > DiscussPhotoConstants.MaximumPhotosPerOwner)
            return (DiscussPhotoUploadStatus.ValidationError, Array.Empty<DiscussPhotoDto>());

        var validatedFiles = new List<(DiscussPhotoUploadFile File, ImageContentValidationResult Validation)>(files.Count);
        foreach (var file in files)
        {
            var validation = await ImageContentValidator.ValidateAsync(file.Content, file.FileName, file.Length, cancellationToken);
            if (!validation.IsValid)
                return (DiscussPhotoUploadStatus.ValidationError, Array.Empty<DiscussPhotoDto>());

            validatedFiles.Add((file, validation));
        }

        var keyPrefix = ResolveObjectKeyPrefix(RequireOrganizationId(), ownerType);
        var nextOrderIndex = existingCount;
        var createdAt = DateTime.UtcNow;
        var uploadedKeys = new List<string>(validatedFiles.Count);

        foreach (var (file, validation) in validatedFiles)
        {
            var photoId = Guid.NewGuid();
            var objectKey = $"{keyPrefix}/{ownerId}/{photoId}{validation.Extension}";

            await _objectStorage.PutAsync(objectKey, file.Content, validation.ContentType, cancellationToken);
            uploadedKeys.Add(objectKey);

            _databaseContext.DiscussPhotos.Add(new DiscussPhoto
            {
                Id = photoId,
                OwnerType = ownerType,
                OwnerId = ownerId,
                ObjectKey = objectKey,
                ContentType = validation.ContentType,
                OrderIndex = nextOrderIndex,
                SizeBytes = file.Length,
                CreatedAt = createdAt
            });

            nextOrderIndex += 1;
        }

        try
        {
            await _databaseContext.SaveChangesAsync(cancellationToken);
            await scope.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            foreach (var key in uploadedKeys)
                await DeleteObjectBestEffortAsync(key, cancellationToken);
            throw;
        }

        var photos = await LoadOrderedPhotosAsync(ownerType, ownerId, cancellationToken);
        return (DiscussPhotoUploadStatus.Success, photos);
    }

    /// <summary>
    /// Removes a photo the acting user authored. The row goes first and the object afterwards, on a
    /// best-effort basis: a stored object nothing points at is waste, while a row pointing at a
    /// deleted object is a broken image on somebody's screen.
    /// </summary>
    public async Task<DiscussOperationStatus> DeletePhotoAsync(Guid photoId, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginWriteAsync(_databaseContext, cancellationToken);

        var photo = await _databaseContext.DiscussPhotos.FirstOrDefaultAsync(candidate => candidate.Id == photoId, cancellationToken);
        if (photo is null)
            return DiscussOperationStatus.NotFound;

        var ownerAuthorId = await ResolveOwnerAuthorIdAsync(photo.OwnerType, photo.OwnerId, cancellationToken);
        if (ownerAuthorId is null)
            return DiscussOperationStatus.NotFound;

        if (ownerAuthorId.Value != actingUserId)
            return DiscussOperationStatus.Forbidden;

        var objectKey = photo.ObjectKey;
        _databaseContext.DiscussPhotos.Remove(photo);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);

        try
        {
            await _objectStorage.DeleteAsync(objectKey, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete discuss photo object {ObjectKey}", objectKey);
        }

        return DiscussOperationStatus.Success;
    }

    /// <summary>
    /// Streams a photo's bytes. The object key is read from a row this organization can see, so the
    /// tenant boundary for object storage is this query and not the key — the key is namespaced as
    /// well (see <see cref="ResolveObjectKeyPrefix"/>) for the benefit of whoever reads a bucket
    /// listing without a database to join against.
    ///
    /// <para>
    /// A row whose object has gone missing reads as no photo at all rather than as an error: the
    /// endpoint serving this is anonymous, and a failure there is a broken image, not an incident.
    /// </para>
    /// </summary>
    public async Task<(Stream Content, string ContentType)?> GetPhotoContentAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(_databaseContext, cancellationToken);

        var photo = await _databaseContext.DiscussPhotos.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == photoId, cancellationToken);

        if (photo is null)
            return null;

        try
        {
            var content = await _objectStorage.GetAsync(photo.ObjectKey, cancellationToken);
            return (content, photo.ContentType);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load discuss photo object {ObjectKey}", photo.ObjectKey);
            return null;
        }
    }

    private async Task<Guid?> ResolveOwnerAuthorIdAsync(
        DiscussPhotoOwner ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        if (ownerType == DiscussPhotoOwner.Thread)
        {
            var thread = await _databaseContext.DiscussThreads.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken);
            return thread?.AuthorId;
        }

        var reply = await _databaseContext.DiscussReplies.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == ownerId, cancellationToken);
        return reply?.AuthorId;
    }

    private async Task<IReadOnlyList<DiscussPhotoDto>> LoadOrderedPhotosAsync(
        DiscussPhotoOwner ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var photos = await _databaseContext.DiscussPhotos.AsNoTracking()
            .Where(photo => photo.OwnerType == ownerType && photo.OwnerId == ownerId)
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(cancellationToken);

        return photos
            .Select(photo => new DiscussPhotoDto(photo.Id, Services.DiscussPhotoUrlBuilder.Build(photo.Id), photo.OrderIndex))
            .ToList();
    }

    private async Task<(IReadOnlyList<DiscussPhotoDto> ThreadPhotos, IReadOnlyDictionary<Guid, IReadOnlyList<DiscussPhotoDto>> ReplyPhotosByReplyId)> LoadThreadAndReplyPhotosAsync(
        Guid threadId, IReadOnlyList<Guid> replyIds, CancellationToken cancellationToken)
    {
        var photos = await _databaseContext.DiscussPhotos.AsNoTracking()
            .Where(photo =>
                (photo.OwnerType == DiscussPhotoOwner.Thread && photo.OwnerId == threadId)
                || (photo.OwnerType == DiscussPhotoOwner.Reply && replyIds.Contains(photo.OwnerId)))
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(cancellationToken);

        var threadPhotos = photos
            .Where(photo => photo.OwnerType == DiscussPhotoOwner.Thread)
            .Select(MapPhotoToDto)
            .ToList();

        var replyPhotosByReplyId = photos
            .Where(photo => photo.OwnerType == DiscussPhotoOwner.Reply)
            .GroupBy(photo => photo.OwnerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DiscussPhotoDto>)group.Select(MapPhotoToDto).ToList());

        return (threadPhotos, replyPhotosByReplyId);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DiscussPhoto>>> LoadThreadPhotosByThreadIdAsync(
        IReadOnlyList<Guid> threadIds, CancellationToken cancellationToken)
    {
        if (threadIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<DiscussPhoto>>();

        var photos = await _databaseContext.DiscussPhotos.AsNoTracking()
            .Where(photo => photo.OwnerType == DiscussPhotoOwner.Thread && threadIds.Contains(photo.OwnerId))
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(cancellationToken);

        return photos
            .GroupBy(photo => photo.OwnerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DiscussPhoto>)group.ToList());
    }

    private async Task DeleteObjectBestEffortAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await _objectStorage.DeleteAsync(objectKey, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete discuss photo object {ObjectKey}", objectKey);
        }
    }

    private static DiscussPhotoDto MapPhotoToDto(DiscussPhoto photo) =>
        new(photo.Id, Services.DiscussPhotoUrlBuilder.Build(photo.Id), photo.OrderIndex);

    /// <summary>
    /// Phase 40.13. New object keys are namespaced by organization:
    /// <c>org/{organizationId:N}/discuss/threads/{ownerId}/{photoId}.jpg</c>.
    ///
    /// <para>
    /// This is not what enforces the boundary — every read loads the <c>DiscussPhotos</c> row first
    /// and that row is behind the query filter and the RLS policy, so a key alone is never enough to
    /// reach an object. It buys the two things a bucket cannot get from Postgres: an operator (or a
    /// bucket lifecycle rule, or a per-customer deletion request) can tell whose file an object is
    /// without a database, and a future per-organization bucket policy has a prefix to attach to.
    /// It mirrors the <c>org:{orgId}:</c> Redis convention 40.11 established, in the shape S3 uses.
    /// </para>
    ///
    /// <para>
    /// Keys written before this block keep their old un-prefixed shape and are still served: the key
    /// is read from the row, never recomputed. No object is moved, and no backfill exists — renaming
    /// live objects would be an operation on live infrastructure for zero correctness gain.
    /// </para>
    /// </summary>
    private static string ResolveObjectKeyPrefix(Guid organizationId, DiscussPhotoOwner ownerType)
    {
        var ownerPrefix = ownerType == DiscussPhotoOwner.Thread
            ? DiscussPhotoConstants.ThreadObjectKeyPrefix
            : DiscussPhotoConstants.ReplyObjectKeyPrefix;

        return $"{DiscussPhotoConstants.OrganizationObjectKeyPrefix}/{organizationId:N}/{ownerPrefix}";
    }
}
