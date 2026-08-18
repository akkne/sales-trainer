namespace Sellevate.Social.Infrastructure.Configuration;

/// <summary>
/// Connection settings for the photo bucket. <c>AccessKey</c> and <c>SecretKey</c> are secrets and
/// arrive from the environment; everything else is committed configuration.
///
/// <para>
/// <c>ForcePathStyle</c> defaults to <see langword="true"/> for MinIO, which does not serve
/// virtual-host-style bucket URLs. A real AWS endpoint needs it set to <see langword="false"/>.
/// </para>
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
