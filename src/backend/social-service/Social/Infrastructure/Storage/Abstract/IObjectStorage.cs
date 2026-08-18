namespace Sellevate.Social.Infrastructure.Storage.Abstract;

/// <summary>
/// The blob store behind discussion photos, addressed by an opaque object key.
///
/// <para>
/// Keys are namespaced by organization (see <c>DiscussService.ResolveObjectKeyPrefix</c>) but this
/// abstraction enforces nothing about tenancy: it will read or delete any key it is handed. The tenant
/// boundary is the database row the key was read from, so a caller must never build a key from
/// user input.
/// </para>
/// </summary>
public interface IObjectStorage
{
    /// <summary>Creates the bucket if it is missing. Idempotent, and called once at startup.</summary>
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes or overwrites an object. The stream is left open for the caller to dispose.
    /// </summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// The object's bytes, buffered in memory and positioned at the start. Throws when the key does
    /// not exist.
    /// </summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes an object; deleting a key that is already gone is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
