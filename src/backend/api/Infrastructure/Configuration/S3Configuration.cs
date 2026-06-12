namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class S3Configuration
{
    public const string SectionName = "Storage:S3";

    public required string Endpoint { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public string Region { get; init; } = "us-east-1";
    public bool ForcePathStyle { get; init; } = true;
}
