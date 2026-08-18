namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// Connection settings for the avatar object store. <c>AccessKey</c> and <c>SecretKey</c> are secrets
/// supplied by the environment. <c>ForcePathStyle</c> defaults to <see langword="true"/> because MinIO
/// serves path-style addresses; virtual-host style would need per-bucket DNS.
/// </summary>
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
