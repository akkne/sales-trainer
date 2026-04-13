using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog.Models;

namespace SalesTrainer.Api.Infrastructure.Mongo;

public class MongoDbContext
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
