namespace Sellevate.Social.Infrastructure.Configuration;

public sealed class MongoConfiguration
{
    public const string SectionName = "Mongo";

    public required string DatabaseName { get; init; }
}
