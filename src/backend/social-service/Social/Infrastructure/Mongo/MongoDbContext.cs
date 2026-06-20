using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Infrastructure.Configuration;

namespace Sellevate.Social.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    private const string ChatConversationsCollectionName = "chat_conversations";

    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient mongoClient, IOptions<MongoConfiguration> mongoConfiguration)
    {
        _database = mongoClient.GetDatabase(mongoConfiguration.Value.DatabaseName);
    }

    public IMongoCollection<ChatConversation> ChatConversations =>
        _database.GetCollection<ChatConversation>(ChatConversationsCollectionName);
}
