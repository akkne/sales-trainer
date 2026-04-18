using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Friends.Models;

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

    public IMongoCollection<ChatConversation> ChatConversations =>
        _database.GetCollection<ChatConversation>("chat_conversations");
}
