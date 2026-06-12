using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Discuss.Constants;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Services.Implementation;

public sealed partial class DiscussService
{
    public async Task<(DiscussPhotoUploadStatus Status, IReadOnlyList<DiscussPhotoDto> Photos)> UploadPhotosAsync(
        DiscussPhotoOwner ownerType,
        Guid ownerId,
        Guid actingUserId,
        IReadOnlyList<DiscussPhotoUploadFile> files,
        CancellationToken ct = default)
    {
        var ownerAuthorId = await ResolveOwnerAuthorIdAsync(ownerType, ownerId, ct);
        if (ownerAuthorId is null)
            return (DiscussPhotoUploadStatus.OwnerNotFound, Array.Empty<DiscussPhotoDto>());

        if (ownerAuthorId.Value != actingUserId)
            return (DiscussPhotoUploadStatus.Forbidden, Array.Empty<DiscussPhotoDto>());

        var existingCount = await _db.DiscussPhotos
            .CountAsync(photo => photo.OwnerType == ownerType && photo.OwnerId == ownerId, ct);

        if (existingCount + files.Count > DiscussPhotoConstants.MaximumPhotosPerOwner)
            return (DiscussPhotoUploadStatus.ValidationError, Array.Empty<DiscussPhotoDto>());

        var validatedFiles = new List<(DiscussPhotoUploadFile File, ImageContentValidationResult Validation)>(files.Count);
        foreach (var file in files)
        {
            var validation = await ImageContentValidator.ValidateAsync(file.Content, file.FileName, file.Length, ct);
            if (!validation.IsValid)
                return (DiscussPhotoUploadStatus.ValidationError, Array.Empty<DiscussPhotoDto>());

            validatedFiles.Add((file, validation));
        }

        var keyPrefix = ResolveObjectKeyPrefix(ownerType);
        var nextOrderIndex = existingCount;
        var createdAt = DateTime.UtcNow;

        foreach (var (file, validation) in validatedFiles)
        {
            var photoId = Guid.NewGuid();
            var objectKey = $"{keyPrefix}/{ownerId}/{photoId}{validation.Extension}";

            await _objectStorage.PutAsync(objectKey, file.Content, validation.ContentType, ct);

            _db.DiscussPhotos.Add(new DiscussPhoto
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

        await _db.SaveChangesAsync(ct);

        var photos = await LoadOrderedPhotosAsync(ownerType, ownerId, ct);
        return (DiscussPhotoUploadStatus.Success, photos);
    }

    public async Task<DiscussOperationStatus> DeletePhotoAsync(Guid photoId, Guid actingUserId, CancellationToken ct = default)
    {
        var photo = await _db.DiscussPhotos.FirstOrDefaultAsync(candidate => candidate.Id == photoId, ct);
        if (photo is null)
            return DiscussOperationStatus.NotFound;

        var ownerAuthorId = await ResolveOwnerAuthorIdAsync(photo.OwnerType, photo.OwnerId, ct);
        if (ownerAuthorId is null)
            return DiscussOperationStatus.NotFound;

        if (ownerAuthorId.Value != actingUserId)
            return DiscussOperationStatus.Forbidden;

        var objectKey = photo.ObjectKey;
        _db.DiscussPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _objectStorage.DeleteAsync(objectKey, ct);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete discuss photo object {ObjectKey}", objectKey);
        }

        return DiscussOperationStatus.Success;
    }

    public async Task<(Stream Content, string ContentType)?> GetPhotoContentAsync(Guid photoId, CancellationToken ct = default)
    {
        var photo = await _db.DiscussPhotos.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == photoId, ct);

        if (photo is null)
            return null;

        try
        {
            var content = await _objectStorage.GetAsync(photo.ObjectKey, ct);
            return (content, photo.ContentType);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load discuss photo object {ObjectKey}", photo.ObjectKey);
            return null;
        }
    }

    private async Task<Guid?> ResolveOwnerAuthorIdAsync(
        DiscussPhotoOwner ownerType, Guid ownerId, CancellationToken ct)
    {
        if (ownerType == DiscussPhotoOwner.Thread)
        {
            var thread = await _db.DiscussThreads.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == ownerId, ct);
            return thread?.AuthorId;
        }

        var reply = await _db.DiscussReplies.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == ownerId, ct);
        return reply?.AuthorId;
    }

    private async Task<IReadOnlyList<DiscussPhotoDto>> LoadOrderedPhotosAsync(
        DiscussPhotoOwner ownerType, Guid ownerId, CancellationToken ct)
    {
        var photos = await _db.DiscussPhotos.AsNoTracking()
            .Where(photo => photo.OwnerType == ownerType && photo.OwnerId == ownerId)
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(ct);

        return photos
            .Select(photo => new DiscussPhotoDto(photo.Id, DiscussPhotoUrlBuilder.Build(photo.Id), photo.OrderIndex))
            .ToList();
    }

    private async Task<(IReadOnlyList<DiscussPhotoDto> ThreadPhotos, IReadOnlyDictionary<Guid, IReadOnlyList<DiscussPhotoDto>> ReplyPhotosByReplyId)> LoadThreadAndReplyPhotosAsync(
        Guid threadId, IReadOnlyList<Guid> replyIds, CancellationToken ct)
    {
        var photos = await _db.DiscussPhotos.AsNoTracking()
            .Where(photo =>
                (photo.OwnerType == DiscussPhotoOwner.Thread && photo.OwnerId == threadId)
                || (photo.OwnerType == DiscussPhotoOwner.Reply && replyIds.Contains(photo.OwnerId)))
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(ct);

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
        IReadOnlyList<Guid> threadIds, CancellationToken ct)
    {
        if (threadIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<DiscussPhoto>>();

        var photos = await _db.DiscussPhotos.AsNoTracking()
            .Where(photo => photo.OwnerType == DiscussPhotoOwner.Thread && threadIds.Contains(photo.OwnerId))
            .OrderBy(photo => photo.OrderIndex)
            .ToListAsync(ct);

        return photos
            .GroupBy(photo => photo.OwnerId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DiscussPhoto>)group.ToList());
    }

    private static DiscussPhotoDto MapPhotoToDto(DiscussPhoto photo) =>
        new(photo.Id, DiscussPhotoUrlBuilder.Build(photo.Id), photo.OrderIndex);

    private static string ResolveObjectKeyPrefix(DiscussPhotoOwner ownerType) => ownerType == DiscussPhotoOwner.Thread
        ? DiscussPhotoConstants.ThreadObjectKeyPrefix
        : DiscussPhotoConstants.ReplyObjectKeyPrefix;
}
