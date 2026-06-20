using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Storage.Abstract;

namespace Sellevate.Identity.Infrastructure.Storage.Implementation;

public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3ObjectStorage(S3Configuration configuration)
    {
        _bucket = configuration.Bucket;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(configuration.AccessKey, configuration.SecretKey),
            BuildClientConfig(configuration));
    }

    public static AmazonS3Config BuildClientConfig(S3Configuration configuration) =>
        new AmazonS3Config
        {
            ServiceURL = configuration.Endpoint,
            ForcePathStyle = configuration.ForcePathStyle,
            AuthenticationRegion = configuration.Region
        };

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
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);

        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception amazonS3Exception) when (amazonS3Exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<string?> TryGetETagAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_bucket, key, cancellationToken);
            return metadata.ETag;
        }
        catch (AmazonS3Exception amazonS3Exception) when (amazonS3Exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_bucket, key, cancellationToken);
    }
}
