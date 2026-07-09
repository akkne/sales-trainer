using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class CompanyCallContext
{
    [BsonElement("companyName")]
    public string CompanyName { get; set; } = null!;

    [BsonElement("companyDescription")]
    public string CompanyDescription { get; set; } = null!;

    [BsonElement("callGoal")]
    public string? CallGoal { get; set; }
}
