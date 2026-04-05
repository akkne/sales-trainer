using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog;

namespace SalesTrainer.Api.Infrastructure.Mongo;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient mongoClient, IConfiguration configuration)
    {
        var databaseName = configuration["Mongo:DatabaseName"] ?? "salestrainer";
        _database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoCollection<DialogSession> DialogSessions =>
        _database.GetCollection<DialogSession>("dialog_sessions");
}
