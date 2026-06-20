namespace Sellevate.Identity.Infrastructure.Storage.Abstract;

public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the object's ETag (content identity) for cache validation, or null if it does not exist.
    /// </summary>
    Task<string?> TryGetETagAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
