using MongoDB.Driver;
using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient mongoClient, IConfiguration configuration)
    {
        var databaseName = configuration["Mongo:DatabaseName"] ?? "sallevate";
        _database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoCollection<DialogSession> DialogSessions =>
        _database.GetCollection<DialogSession>("dialog_sessions");
}
