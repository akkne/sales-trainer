using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Storage.Implementation;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class S3ObjectStorageConfigTests
{
    [Test]
    public void BuildClientConfig_SetsServiceUrlAndForcePathStyle()
    {
        var config = new S3Configuration
        {
            Endpoint = "http://localhost:9000",
            Bucket = "test-bucket",
            AccessKey = "access",
            SecretKey = "secret",
            Region = "us-east-1",
            ForcePathStyle = true
        };

        var clientConfig = S3ObjectStorage.BuildClientConfig(config);

        clientConfig.ServiceURL.TrimEnd('/').Should().Be("http://localhost:9000");
        clientConfig.ForcePathStyle.Should().BeTrue();
        clientConfig.AuthenticationRegion.Should().Be("us-east-1");
    }

    [Test]
    public void S3Configuration_BindsFromInMemoryConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Storage:S3:Endpoint"] = "http://minio:9000",
            ["Storage:S3:Bucket"] = "salestrainer-avatars",
            ["Storage:S3:AccessKey"] = "minioadmin",
            ["Storage:S3:SecretKey"] = "minioadmin",
            ["Storage:S3:Region"] = "us-east-1",
            ["Storage:S3:ForcePathStyle"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var s3Config = new S3Configuration
        {
            Endpoint = configuration["Storage:S3:Endpoint"]!,
            Bucket = configuration["Storage:S3:Bucket"]!,
            AccessKey = configuration["Storage:S3:AccessKey"]!,
            SecretKey = configuration["Storage:S3:SecretKey"]!,
            Region = configuration["Storage:S3:Region"] ?? "us-east-1",
            ForcePathStyle = bool.Parse(configuration["Storage:S3:ForcePathStyle"] ?? "true")
        };

        s3Config.Endpoint.Should().Be("http://minio:9000");
        s3Config.Bucket.Should().Be("salestrainer-avatars");
        s3Config.AccessKey.Should().Be("minioadmin");
        s3Config.SecretKey.Should().Be("minioadmin");
        s3Config.Region.Should().Be("us-east-1");
        s3Config.ForcePathStyle.Should().BeTrue();
    }

    [Test]
    public void S3Configuration_DefaultRegionAndForcePathStyle()
    {
        var config = new S3Configuration
        {
            Endpoint = "http://localhost:9000",
            Bucket = "bucket",
            AccessKey = "key",
            SecretKey = "secret"
        };

        config.Region.Should().Be("us-east-1");
        config.ForcePathStyle.Should().BeTrue();
    }
}
