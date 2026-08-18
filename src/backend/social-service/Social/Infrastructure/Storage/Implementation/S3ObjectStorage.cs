using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Sellevate.Social.Infrastructure.Configuration;
using Sellevate.Social.Infrastructure.Storage.Abstract;

namespace Sellevate.Social.Infrastructure.Storage.Implementation;

/// <summary>
/// S3-compatible object storage — MinIO in every environment the platform runs today, which is why
/// path-style addressing and an explicit endpoint are configurable rather than assumed.
///
/// <para>
/// Registered as a singleton: the underlying client is thread-safe, holds the connection pool, and is
/// deliberately built once. It carries no tenant state, so a singleton is safe here in a way it would
/// not be for anything reading <c>ITenantContext</c>.
/// </para>
/// </summary>
internal sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3ObjectStorage(IOptions<S3Configuration> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configuration = options.Value;
        _bucket = configuration.Bucket;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(configuration.AccessKey, configuration.SecretKey),
            BuildClientConfiguration(configuration));
    }

    private static AmazonS3Config BuildClientConfiguration(S3Configuration configuration) =>
        new AmazonS3Config
        {
            ServiceURL = configuration.Endpoint,
            ForcePathStyle = configuration.ForcePathStyle,
            AuthenticationRegion = configuration.Region
        };

    /// <summary>
    /// Creates the bucket, treating "it already exists and is mine" as success — two services booting
    /// against the same MinIO must both survive the race.
    /// </summary>
    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _bucket,
                UseClientRegion = true
            }, cancellationToken);
        }
        catch (AmazonS3Exception amazonS3Exception) when (
            amazonS3Exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(putObjectRequest, cancellationToken);
    }

    /// <summary>
    /// Copies the object into memory rather than handing back the network stream, so the response is
    /// not still reading from storage after this method's cancellation token is gone.
    /// </summary>
    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);

        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
    }
}
