namespace Sellevate.Identity.Infrastructure.Storage.Abstract;

public interface IObjectStorage
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task<string?> TryGetETagAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
