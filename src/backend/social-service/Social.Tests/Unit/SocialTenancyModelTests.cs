using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Chat.Services.Implementation;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Discuss.Services.Implementation;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Configuration;
using Sellevate.Social.Infrastructure.Mongo;
using Sellevate.Social.Infrastructure.Storage.Abstract;
using Sellevate.Social.Tests.Helpers;

namespace Sellevate.Social.Tests.Unit;

/// <summary>
/// Phase 40.13. Fast, infrastructure-free guards for the tenancy mistakes that are easiest to make
/// in social-service and hardest to notice. The real isolation boundary is Postgres row-level
/// security (friendships, threads, replies, votes, thread-tags, photos) and
/// <see cref="ChatConversationRepository"/> (Mongo, which has no RLS); both are exercised against
/// real stores by <c>Integration/SocialTenantIsolationIntegrationTests</c>. These are the cheap
/// tripwires that run on every build.
/// </summary>
[TestFixture]
public class SocialTenancyModelTests
{
    private static readonly Guid OrganizationAId = new("a0000000-0000-4000-8000-0000000000a1");
    private static readonly Guid OrganizationBId = new("b0000000-0000-4000-8000-0000000000b2");

    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 names explicitly: EF query filters are NOT inherited
    /// through navigations, so a filter on DiscussThread says nothing about DiscussReply even though
    /// every read composes Thread -> Replies. Rather than listing the entities again here (which
    /// would just repeat whichever omission it is meant to catch), this walks the model and demands
    /// a filter from anything carrying an OrganizationId.
    /// </summary>
    [Test]
    public void Every_entity_with_an_organization_id_has_its_own_query_filter()
    {
        using var databaseContext = TestSocialDatabaseFactory.CreateInMemory();

        var entitiesMissingAFilter = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        entitiesMissingAFilter.Should().BeEmpty();
    }

    [Test]
    public void Social_rows_are_tenant_data_and_tags_are_content()
    {
        // Somebody saying something to their colleagues. There is no global friendship, thread,
        // reply, vote or photo.
        typeof(Friendship).Should().BeAssignableTo<ITenantScoped>();
        typeof(DiscussThread).Should().BeAssignableTo<ITenantScoped>();
        typeof(DiscussReply).Should().BeAssignableTo<ITenantScoped>();
        typeof(DiscussVote).Should().BeAssignableTo<ITenantScoped>();
        typeof(DiscussThreadTag).Should().BeAssignableTo<ITenantScoped>();
        typeof(DiscussPhoto).Should().BeAssignableTo<ITenantScoped>();

        // A tag is a word. NULL is meaningful — it is the curated vocabulary every organization
        // shares — so it cannot implement an interface whose OrganizationId is non-nullable.
        typeof(DiscussTag).Should().NotBeAssignableTo<ITenantScoped>();
    }

    /// <summary>
    /// The roadmap's explicit call for 40.13: friendship must not cross the organization boundary.
    /// This is the half that lives in Postgres — one organization's friendship row is not visible to
    /// another, so it can never be accepted from the other side and never becomes a friendship at
    /// all.
    /// </summary>
    [Test]
    public async Task A_friendship_created_in_one_organization_is_invisible_in_another()
    {
        var databaseName = Guid.NewGuid().ToString();
        var requesterId = Guid.NewGuid();
        var addresseeId = Guid.NewGuid();

        await using (var organizationAContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationAId))
        {
            organizationAContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                RequesterId = requesterId,
                AddresseeId = addresseeId,
                Status = FriendshipStatus.Accepted,
                CreatedAt = DateTime.UtcNow,
                AcceptedAt = DateTime.UtcNow
            });
            await organizationAContext.SaveChangesAsync();
        }

        await using var organizationBContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationBId);

        var visibleToB = await organizationBContext.Friendships.ToListAsync();
        var visibleToBIgnoringFilters = await organizationBContext.Friendships.IgnoreQueryFilters().ToListAsync();

        visibleToB.Should().BeEmpty();

        // The row exists, it is simply not this organization's. Asserting the second half is what
        // distinguishes "the filter works" from "the test seeded nothing".
        visibleToBIgnoringFilters.Should().ContainSingle()
            .Which.OrganizationId.Should().Be(OrganizationAId);
    }

    /// <summary>
    /// Chat's boundary is the friendship, not a second check: <c>ChatService</c> refuses to open a
    /// conversation between two people who are not accepted friends, and the friendship is
    /// tenant-scoped. So the Mongo side never gets the chance to create a cross-tenant conversation.
    /// </summary>
    [Test]
    public async Task Chat_cannot_be_opened_across_the_organization_boundary()
    {
        var databaseName = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();

        await using (var organizationAContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationAId))
        {
            organizationAContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                RequesterId = userId,
                AddresseeId = friendUserId,
                Status = FriendshipStatus.Accepted,
                CreatedAt = DateTime.UtcNow,
                AcceptedAt = DateTime.UtcNow
            });
            await organizationAContext.SaveChangesAsync();
        }

        await using var organizationBContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationBId);
        var chatService = new ChatService(
            conversations: Substitute.For<IChatConversationRepository>(),
            organizationBContext,
            new RecordingSocialEventPublisher());

        var act = async () => await chatService.GetOrCreateConversationAsync(userId, friendUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You can only chat with accepted friends.");
    }

    /// <summary>
    /// Curated tags stay global and are visible everywhere; a tag one organization typed is not.
    /// Plain equality in the query filter would hand a new customer an empty tag picker, and no
    /// filter at all would hand them somebody else's product names.
    /// </summary>
    [Test]
    public async Task Curated_tags_are_shared_and_an_organizations_own_tag_is_not()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var seedContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationAId))
        {
            seedContext.DiscussTags.Add(NewTag("objections", organizationId: null, isCurated: true));
            seedContext.DiscussTags.Add(NewTag("owned-by-a", OrganizationAId, isCurated: false));
            seedContext.DiscussTags.Add(NewTag("owned-by-b", OrganizationBId, isCurated: false));
            await seedContext.SaveChangesAsync();
        }

        await using var organizationAContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationAId);
        var visibleToA = await organizationAContext.DiscussTags.Select(tag => tag.Slug).ToListAsync();

        await using var organizationBContext = TestSocialDatabaseFactory.CreateInMemory(databaseName, OrganizationBId);
        var visibleToB = await organizationBContext.DiscussTags.Select(tag => tag.Slug).ToListAsync();

        visibleToA.Should().BeEquivalentTo("objections", "owned-by-a");
        visibleToB.Should().BeEquivalentTo("objections", "owned-by-b");
    }

    /// <summary>
    /// The stamp nothing else can apply: <c>DiscussTag.OrganizationId</c> is nullable, so the write
    /// guard skips it entirely. A tag typed while starting a thread must still land in the author's
    /// organization rather than in the global vocabulary.
    /// </summary>
    [Test]
    public async Task A_tag_typed_by_a_user_belongs_to_their_organization()
    {
        await using var databaseContext = TestSocialDatabaseFactory.CreateInMemory(OrganizationAId);
        var discussService = BuildDiscussService(databaseContext, OrganizationAId);

        await discussService.CreateThreadAsync(
            Guid.NewGuid(), "Как отрабатывать возражение по цене", "Body", ["Наш продукт"]);

        var tags = await databaseContext.DiscussTags.IgnoreQueryFilters().ToListAsync();

        tags.Should().ContainSingle()
            .Which.OrganizationId.Should().Be(OrganizationAId,
                "a tag one customer typed must never end up in the vocabulary every other customer sees");
    }

    /// <summary>
    /// Object keys carry the organization so an operator staring at a bucket listing — who has no
    /// database to join against — can tell whose file an object is.
    /// </summary>
    [Test]
    public async Task Photo_object_keys_are_namespaced_by_organization()
    {
        await using var databaseContext = TestSocialDatabaseFactory.CreateInMemory(OrganizationAId);
        var objectStorage = Substitute.For<IObjectStorage>();
        var discussService = BuildDiscussService(databaseContext, OrganizationAId, objectStorage);

        var authorId = Guid.NewGuid();
        var thread = await discussService.CreateThreadAsync(authorId, "Title", "Body", []);

        var (status, _) = await discussService.UploadPhotosAsync(
            DiscussPhotoOwner.Thread,
            thread.Id,
            authorId,
            [new DiscussPhotoUploadFile(new MemoryStream(OnePixelPng), "photo.png", OnePixelPng.Length)]);

        status.Should().Be(DiscussPhotoUploadStatus.Success);

        var photo = await databaseContext.DiscussPhotos.IgnoreQueryFilters().SingleAsync();
        photo.ObjectKey.Should().StartWith($"org/{OrganizationAId:N}/discuss/threads/");
        photo.OrganizationId.Should().Be(OrganizationAId);
    }

    /// <summary>
    /// Roadmap 40.14: "an unset tenant is an exception, never a licence". Mongo has no policy to
    /// fall back on, so the repository has to be the thing that refuses — and it has to refuse on
    /// <em>every</em> method, not on the ones somebody remembered. This walks the interface instead
    /// of listing methods, so a method added later without the guard fails the build.
    /// </summary>
    [Test]
    public async Task Every_conversation_repository_method_refuses_an_unset_tenant()
    {
        var repository = BuildRepositoryWithoutTenant();
        var methods = typeof(IChatConversationRepository).GetMethods();

        methods.Should().NotBeEmpty();

        foreach (var method in methods)
        {
            var arguments = method.GetParameters().Select(BuildArgument).ToArray();

            var act = async () =>
            {
                var invocationResult = method.Invoke(repository, arguments);
                await (Task)invocationResult!;
            };

            (await act.Should().ThrowAsync<InvalidOperationException>(
                    "{0} must refuse an unset tenant rather than widening to every organization",
                    method.Name))
                .WithMessage("Organization context is not set.");
        }
    }

    /// <summary>
    /// The repository is only a boundary while it is the sole holder of the collection. Nothing in
    /// C# stops a future service from calling <c>GetCollection&lt;ChatConversation&gt;</c> itself and
    /// quietly reintroducing an un-scoped read, so this asserts on the source tree: exactly one
    /// production file may name the collection.
    /// </summary>
    [Test]
    public void Only_the_repository_reaches_the_chat_conversations_collection()
    {
        var serviceRoot = FindSocialServiceSourceRoot();
        if (serviceRoot is null)
        {
            Assert.Ignore("social-service sources are not next to the test assembly; nothing to scan.");
            return;
        }

        var offendingFiles = Directory
            .EnumerateFiles(serviceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("GetCollection<ChatConversation>", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        offendingFiles.Should().BeEquivalentTo([$"{nameof(ChatConversationRepository)}.cs"],
            "the tenant filter for Mongo is application-level, so it is only auditable while there is one place to audit");
    }

    /// <summary>
    /// notification-service consumes all five of social's topics and requires the organization on
    /// the envelope. An event published without one is a notification that dead-letters instead of
    /// reaching anybody, so the publisher refuses rather than emitting it.
    /// </summary>
    [Test]
    public async Task Published_events_carry_the_organization_and_refuse_an_unset_tenant()
    {
        var recordingPublisher = new RecordingEnvelopePublisher();

        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(OrganizationAId);
        var publisher = new KafkaSocialEventPublisher(recordingPublisher, tenantContext);

        await publisher.PublishFriendRequestReceivedAsync(
            new FriendRequestReceivedEvent(Guid.NewGuid(), "Requester", Guid.NewGuid(), Guid.NewGuid()));

        recordingPublisher.OrganizationIds.Should().BeEquivalentTo([OrganizationAId]);

        var publisherWithoutTenant = new KafkaSocialEventPublisher(recordingPublisher, new TenantContext());
        var act = async () => await publisherWithoutTenant.PublishChatMessageSentAsync(
            new ChatMessageSentEvent(Guid.NewGuid(), "Sender", "preview", Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization context is not set.");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private static DiscussTag NewTag(string slug, Guid? organizationId, bool isCurated) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Slug = slug,
        Name = slug,
        IsCurated = isCurated,
        CreatedAt = DateTime.UtcNow
    };

    private static DiscussService BuildDiscussService(
        Infrastructure.Data.SocialDbContext databaseContext,
        Guid organizationId,
        IObjectStorage? objectStorage = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        return new DiscussService(
            databaseContext,
            objectStorage ?? Substitute.For<IObjectStorage>(),
            new RecordingSocialEventPublisher(),
            tenantContext,
            NullLogger<DiscussService>.Instance);
    }

    /// <summary>
    /// A repository over a substituted Mongo driver and an empty <see cref="TenantContext"/>. The
    /// substitutes are never reached: every method under test throws before it issues a command,
    /// which is precisely the property being asserted.
    /// </summary>
    private static IChatConversationRepository BuildRepositoryWithoutTenant()
    {
        var mongoDatabase = Substitute.For<IMongoDatabase>();
        mongoDatabase
            .GetCollection<ChatConversation>(Arg.Any<string>(), Arg.Any<MongoCollectionSettings>())
            .Returns(Substitute.For<IMongoCollection<ChatConversation>>());

        var mongoClient = Substitute.For<IMongoClient>();
        mongoClient.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDatabase);

        var mongoConfiguration = Microsoft.Extensions.Options.Options.Create(
            new MongoConfiguration { DatabaseName = "social-tests" });

        return new ChatConversationRepository(new MongoDbContext(mongoClient, mongoConfiguration), new TenantContext());
    }

    private static object? BuildArgument(ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;

        if (parameterType == typeof(string))
        {
            return "conversation-id";
        }

        if (parameterType == typeof(ChatConversation))
        {
            return new ChatConversation { ParticipantIds = [Guid.NewGuid(), Guid.NewGuid()] };
        }

        if (parameterType == typeof(ChatMessage))
        {
            return new ChatMessage();
        }

        if (parameterType == typeof(IReadOnlyList<Guid>))
        {
            return new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        }

        return parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
    }

    /// <summary>
    /// Walks up from this file's compile-time path to the social-service source root. Compile-time,
    /// not runtime, because the test binary lives under bin/ and knows nothing about the tree.
    /// </summary>
    private static string? FindSocialServiceSourceRoot([CallerFilePath] string thisFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(thisFilePath)!);

        while (directory is not null && directory.Name != "social-service")
        {
            directory = directory.Parent;
        }

        var serviceRoot = directory is null ? null : Path.Combine(directory.FullName, "Social");
        return serviceRoot is not null && Directory.Exists(serviceRoot) ? serviceRoot : null;
    }

    /// <summary>The smallest valid PNG the image validator accepts.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class RecordingEnvelopePublisher : IEventPublisher
    {
        public List<Guid?> OrganizationIds { get; } = [];

        public Task PublishAsync<TData>(
            string topic,
            string partitionKey,
            string eventType,
            TData data,
            int version = 1,
            Guid? organizationId = null,
            CancellationToken cancellationToken = default)
        {
            OrganizationIds.Add(organizationId);
            return Task.CompletedTask;
        }
    }
}
