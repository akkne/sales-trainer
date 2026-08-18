namespace Sellevate.Social.Infrastructure.Configuration;

/// <summary>
/// The Mongo database chat conversations live in. The connection string is separate and comes from
/// <c>ConnectionStrings:Mongo</c>, because it carries credentials and this section does not.
/// </summary>
public sealed class MongoConfiguration
{
    public const string SectionName = "Mongo";

    public required string DatabaseName { get; init; }
}
