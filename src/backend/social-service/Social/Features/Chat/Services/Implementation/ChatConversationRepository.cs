using MongoDB.Driver;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Infrastructure.Mongo;

namespace Sellevate.Social.Features.Chat.Services.Implementation;

/// <summary>
/// Phase 40.13. The only code in social-service that touches the <c>chat_conversations</c>
/// collection.
///
/// <para>
/// Two properties make that claim enforceable rather than aspirational, copied from ai-service's
/// 40.11 <c>DialogSessionRepository</c> because it is the same problem. First, the collection handle
/// is created here and nowhere else — <c>MongoDbContext</c> no longer exposes it, so reaching around
/// this class means writing <c>GetCollection&lt;ChatConversation&gt;</c> by hand, which
/// <c>SocialTenancyModelTests</c> turns into a failing build. Second, every filter starts from
/// <see cref="TenantFilter"/>, and <see cref="RequireOrganizationId"/> throws when the tenant is
/// unset, so there is no path through this class that returns another organization's conversations
/// and none that returns all of them.
/// </para>
///
/// <para>
/// This matters more here than anywhere else in Phase 40. A chat message is one person writing to
/// another; a conversation that crosses the organization boundary is the most visible possible
/// failure of multi-tenancy, and Mongo offers nothing to catch it. The second line of defence is
/// upstream and structural rather than in this class: <c>ChatService</c> refuses to open a
/// conversation between two people who are not accepted friends, and friendship rows are
/// Postgres-side tenant data behind an RLS policy — so a conversation that cannot be started
/// cross-tenant is one that cannot exist.
/// </para>
///
/// <para>
/// There is deliberately no system-mode bypass. Nothing in social-service reads conversations
/// outside a request today, so admitting one would be adding the escape hatch this class exists to
/// remove.
/// </para>
/// </summary>
internal sealed class ChatConversationRepository : IChatConversationRepository
{
    /// <summary>
    /// The collection name lives here and only here; see the class remarks. Keep it a literal on
    /// this line — the tripwire test greps for it.
    /// </summary>
    private const string CollectionName = "chat_conversations";

    private readonly IMongoCollection<ChatConversation> _conversations;
    private readonly ITenantContext _tenantContext;

    public ChatConversationRepository(MongoDbContext mongoContext, ITenantContext tenantContext)
    {
        _conversations = mongoContext.Database.GetCollection<ChatConversation>(CollectionName);
        _tenantContext = tenantContext;
    }

    public async Task<ChatConversation?> FindByParticipantsAsync(
        IReadOnlyList<Guid> sortedParticipantIds,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ChatConversation>.Filter.And(
            TenantFilter(),
            Builders<ChatConversation>.Filter.Eq(
                conversation => conversation.ParticipantIds, sortedParticipantIds.ToList()));

        return await _conversations.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ChatConversation?> FindForParticipantAsync(
        string conversationId,
        Guid participantUserId,
        CancellationToken cancellationToken = default)
        => await _conversations
            .Find(ConversationOfParticipantFilter(conversationId, participantUserId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<List<ChatConversation>> ListForParticipantAsync(
        Guid participantUserId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ChatConversation>.Filter.And(
            TenantFilter(),
            Builders<ChatConversation>.Filter.AnyEq(
                conversation => conversation.ParticipantIds, participantUserId));
        var sort = Builders<ChatConversation>.Sort.Descending(conversation => conversation.LastMessageAt);

        return await _conversations.Find(filter).Sort(sort).ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganizationId();

        if (conversation.OrganizationId != Guid.Empty && conversation.OrganizationId != organizationId)
        {
            throw new CrossTenantWriteException(nameof(ChatConversation), organizationId);
        }

        conversation.OrganizationId = organizationId;
        await _conversations.InsertOneAsync(conversation, cancellationToken: cancellationToken);
    }

    public async Task AppendMessageAsync(
        string conversationId,
        Guid senderId,
        ChatMessage message,
        CancellationToken cancellationToken = default)
        => await _conversations.UpdateOneAsync(
            ConversationOfParticipantFilter(conversationId, senderId),
            Builders<ChatConversation>.Update
                .Push(conversation => conversation.Messages, message)
                .Set(conversation => conversation.LastMessageAt, message.SentAt),
            cancellationToken: cancellationToken);

    public async Task SetReadWatermarkAsync(
        string conversationId,
        Guid participantUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
        => await _conversations.UpdateOneAsync(
            ConversationOfParticipantFilter(conversationId, participantUserId),
            // Dotted field path: sets only this participant's entry, leaving the other's watermark
            // untouched.
            Builders<ChatConversation>.Update.Set($"lastReadAt.{participantUserId}", readAt),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Organization AND membership in one filter. Keeping the membership check here rather than in
    /// the caller is the point: a "load the conversation, then check the participant" shape works
    /// until somebody adds a sixth call site and forgets the second half.
    /// </summary>
    private FilterDefinition<ChatConversation> ConversationOfParticipantFilter(
        string conversationId, Guid participantUserId)
        => Builders<ChatConversation>.Filter.And(
            TenantFilter(),
            Builders<ChatConversation>.Filter.Eq(conversation => conversation.Id, conversationId),
            Builders<ChatConversation>.Filter.AnyEq(
                conversation => conversation.ParticipantIds, participantUserId));

    /// <summary>
    /// The one filter every read and write in this class starts from. A conversation is tenant data,
    /// not content: there is no "global conversation", so this is plain equality with no null branch.
    /// </summary>
    private FilterDefinition<ChatConversation> TenantFilter()
        => Builders<ChatConversation>.Filter.Eq(
            conversation => conversation.OrganizationId, RequireOrganizationId());

    /// <summary>
    /// Fails closed and loudly. Returning every conversation for an unset tenant is the exact
    /// failure 40.14 is written to hunt; returning none would hide a misconfigured gateway behind an
    /// empty chat list, which is how a tenancy bug survives to production. The message matches
    /// <c>TenantSaveChangesInterceptor</c>'s word for word so operators grep once.
    /// </summary>
    private Guid RequireOrganizationId()
        => _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");
}
