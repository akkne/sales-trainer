namespace Sellevate.Identity.Infrastructure.Storage.Abstract;

/// <summary>
/// Blob storage for avatar images, keyed by object path. <c>TryGet</c>-shaped members answer with
/// <see langword="null"/> for a missing object rather than throwing, because "no avatar yet" is an
/// ordinary state and not an error.
/// </summary>
public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task<string?> TryGetETagAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
