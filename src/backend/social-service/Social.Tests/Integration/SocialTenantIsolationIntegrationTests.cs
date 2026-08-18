using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Npgsql;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Chat.Services.Implementation;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Configuration;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Infrastructure.Mongo;

namespace Sellevate.Social.Tests.Integration;

/// <summary>
/// Phase 40.13 — the acceptance criteria of the social half of the block, against real stores.
/// social-service's tenant boundary spans two of them and they fail differently: Postgres has
/// row-level security, Mongo has nothing but <see cref="ChatConversationRepository"/>. Both are
/// checked here, with two organizations, and neither seeing the other's anything.
///
/// <para>
/// The Postgres schema is built by running social-service's own EF migrations, not by a hand-written
/// CREATE TABLE script: what is under test is the RLS that migration
/// <c>20260816081204_AddOrganizationId</c> actually emits, not a restatement of it. Reads run as a
/// throwaway NOBYPASSRLS login role, because isolation tested as the superuser proves nothing.
/// </para>
///
/// <para>
/// Runs against the local Docker stack <c>scripts/dev-infra.sh</c> starts (Postgres 5433, Mongo
/// 27017 by default — see <see cref="LocalSocialStoreTestSettings"/>), inside a throwaway Postgres
/// database and role and a throwaway Mongo database. Per CLAUDE.md and docs/DONT_FORGET.md it must
/// never hang or fail the build when the infrastructure is not running: every probe is bounded and
/// every test then skips in seconds. See docs/TESTING/TENANCY.md for the one command that runs it.
/// </para>
///
/// <para>
/// Written but deliberately NOT run by the agent — Правило №2 in docs/DONT_FORGET.md.
/// </para>
/// </summary>
[TestFixture]
[Category("Integration")]
public class SocialTenantIsolationIntegrationTests
{
    private static readonly Guid OrganizationAId = new("a0000000-0000-4000-8000-0000000000a1");
    private static readonly Guid OrganizationBId = new("b0000000-0000-4000-8000-0000000000b2");

    private static readonly Guid OrganizationAThreadId = new("a0000000-0000-4000-8000-0000000000c1");
    private static readonly Guid OrganizationBThreadId = new("b0000000-0000-4000-8000-0000000000c2");
    private static readonly Guid OrganizationAReplyId = new("a0000000-0000-4000-8000-0000000000d1");
    private static readonly Guid OrganizationBReplyId = new("b0000000-0000-4000-8000-0000000000d2");

    private static readonly Guid GlobalTagId = new("00000000-0000-4000-8000-0000000000e0");
    private static readonly Guid OrganizationATagId = new("a0000000-0000-4000-8000-0000000000e1");
    private static readonly Guid OrganizationBTagId = new("b0000000-0000-4000-8000-0000000000e2");

    /// <summary>
    /// Deliberately the same two people on both sides of the boundary: after 40.6 one person can be a
    /// member of two customers, and several assertions below exist only because that is possible.
    /// </summary>
    private static readonly Guid FirstUserId = new("00000000-0000-4000-8000-0000000000f1");
    private static readonly Guid SecondUserId = new("00000000-0000-4000-8000-0000000000f2");

    private bool _isPostgresReachable;
    private bool _isMongoReachable;

    private string _organizationAConversationId = string.Empty;
    private string _organizationBConversationId = string.Empty;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _isPostgresReachable = await LocalSocialStoreTestSettings.IsPostgresReachableAsync();
        _isMongoReachable = await LocalSocialStoreTestSettings.IsMongoReachableAsync();

        if (_isPostgresReachable)
        {
            await SetUpPostgresAsync();
        }

        if (_isMongoReachable)
        {
            await SetUpMongoAsync();
        }
    }

    /// <summary>
    /// Drops the throwaway database, role and Mongo database. Every step is best-effort: the fixture
    /// owns nothing anybody else needs, and failing a run over teardown would hide the result of the
    /// tests that just passed.
    /// </summary>
    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (_isPostgresReachable)
        {
            try
            {
                await using var maintenanceConnection = new NpgsqlConnection(LocalSocialStoreTestSettings.AdminConnectionString());
                await maintenanceConnection.OpenAsync();
                await ExecuteAsync(maintenanceConnection, $"""
                    SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                    WHERE datname = '{LocalSocialStoreTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                    """);
                await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalSocialStoreTestSettings.TestDatabaseName};");
                await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalSocialStoreTestSettings.ApplicationRoleName};");
            }
            catch
            {
            }
        }

        if (_isMongoReachable)
        {
            try
            {
                await new MongoClient(LocalSocialStoreTestSettings.MongoConnectionString())
                    .DropDatabaseAsync(LocalSocialStoreTestSettings.MongoDatabaseName);
            }
            catch
            {
            }
        }
    }

    [Test]
    public async Task One_organizations_threads_are_invisible_to_another()
    {
        SkipIfPostgresIsNotReachable();

        var visibleToA = await ReadVisibleThreadIdsAsync(OrganizationAId);
        var visibleToB = await ReadVisibleThreadIdsAsync(OrganizationBId);

        visibleToA.Should().BeEquivalentTo(new[] { OrganizationAThreadId });
        visibleToB.Should().BeEquivalentTo(new[] { OrganizationBThreadId });
    }

    /// <summary>
    /// The query filter is convenience; the policy is the boundary. Stripping the filter on purpose
    /// is the only way to tell which one is actually doing the work.
    /// </summary>
    [Test]
    public async Task Row_level_security_hides_the_other_organization_even_with_query_filters_ignored()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var threadIds = await context.DiscussThreads.IgnoreQueryFilters().Select(thread => thread.Id).ToListAsync();
        var friendships = await context.Friendships.IgnoreQueryFilters().ToListAsync();

        threadIds.Should().NotContain(OrganizationBThreadId);
        friendships.Should().OnlyContain(friendship => friendship.OrganizationId == OrganizationAId);

        await transaction.CommitAsync();
    }

    [Test]
    public async Task Raw_sql_cannot_reach_the_other_organizations_replies()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var reachableReplyIds = await context.Database
            .SqlQuery<Guid>($"SELECT \"Id\" FROM \"DiscussReplies\"")
            .ToListAsync();

        reachableReplyIds.Should().Contain(OrganizationAReplyId);
        reachableReplyIds.Should().NotContain(OrganizationBReplyId);

        await transaction.CommitAsync();
    }

    /// <summary>
    /// The rule <see cref="TenantTransactionScope"/> exists to enforce, demonstrated rather than
    /// asserted about the source: <c>SET LOCAL</c> only applies inside a transaction, so a read
    /// without one sees nothing at all. This is the fail-closed direction — and the reason every
    /// service method opens a scope as its first statement.
    /// </summary>
    [Test]
    public async Task A_read_outside_a_transaction_sees_nothing_rather_than_everything()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);

        var threadIdsWithoutATransaction = await context.DiscussThreads
            .IgnoreQueryFilters()
            .Select(thread => thread.Id)
            .ToListAsync();

        threadIdsWithoutATransaction.Should().BeEmpty(
            "without SET LOCAL the policy compares against NULL, which matches no row");
    }

    /// <summary>
    /// Two guards fire here and either one is enough: the write interceptor refuses a foreign
    /// organization before the command is built, and the policy's <c>WITH CHECK</c> would refuse the
    /// row if it ever reached Postgres. The assertion is deliberately loose about which spoke first.
    /// </summary>
    [Test]
    public async Task Writing_a_thread_into_another_organization_is_refused_by_the_policy()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);

        context.DiscussThreads.Add(new DiscussThread
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationBId,
            AuthorId = FirstUserId,
            Title = "Smuggled",
            Body = "Should never land",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        });

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task Curated_tags_are_shared_and_an_organizations_own_tag_is_not()
    {
        SkipIfPostgresIsNotReachable();

        var visibleToA = await ReadVisibleTagIdsAsync(OrganizationAId);
        var visibleToB = await ReadVisibleTagIdsAsync(OrganizationBId);

        visibleToA.Should().BeEquivalentTo(new[] { GlobalTagId, OrganizationATagId });
        visibleToB.Should().BeEquivalentTo(new[] { GlobalTagId, OrganizationBTagId });
    }

    /// <summary>
    /// The unique swap this migration performs, from the other end: the same two colleagues at two
    /// customers must be able to be friends in both. Under the pre-40.13 platform-wide
    /// <c>UNIQUE(CanonicalLowId, CanonicalHighId)</c> the second organization's row was rejected as
    /// a duplicate — a friend request that failed for reasons nobody could see.
    /// </summary>
    [Test]
    public async Task The_same_two_people_can_be_friends_in_two_organizations()
    {
        SkipIfPostgresIsNotReachable();

        var friendshipsInA = await ReadFriendshipCountAsync(OrganizationAId);
        var friendshipsInB = await ReadFriendshipCountAsync(OrganizationBId);

        friendshipsInA.Should().Be(1);
        friendshipsInB.Should().Be(1);
    }

    [Test]
    public async Task One_organizations_conversations_are_invisible_to_another()
    {
        SkipIfMongoIsNotReachable();

        var visibleToA = await CreateConversationRepository(OrganizationAId).ListForParticipantAsync(FirstUserId);
        var visibleToB = await CreateConversationRepository(OrganizationBId).ListForParticipantAsync(FirstUserId);

        visibleToA.Should().ContainSingle().Which.Id.Should().Be(_organizationAConversationId);
        visibleToB.Should().ContainSingle().Which.Id.Should().Be(_organizationBConversationId);
    }

    /// <summary>
    /// The 2026-08-16 role split in the store that has no row-level security to express it: what
    /// Postgres does with an <c>OR</c> in a policy, this repository does with an empty filter.
    /// </summary>
    [Test]
    public async Task Platform_staff_see_conversations_from_every_organization()
    {
        SkipIfMongoIsNotReachable();

        var visibleToPlatformStaff = await CreatePlatformStaffConversationRepository()
            .ListForParticipantAsync(FirstUserId);

        visibleToPlatformStaff.Select(conversation => conversation.Id)
            .Should().Contain([_organizationAConversationId, _organizationBConversationId]);
    }

    /// <summary>
    /// Platform mode widens; it does not disable the fail-closed rule. A request with neither an
    /// organization nor platform mode is a misconfigured gateway, and must still throw rather than
    /// quietly return every customer's chat history.
    /// </summary>
    [Test]
    public async Task A_repository_with_no_tenant_at_all_still_refuses_to_read()
    {
        SkipIfMongoIsNotReachable();

        var repositoryWithNoTenant = new ChatConversationRepository(
            CreateMongoContext(), new TenantContext());

        var listing = async () => await repositoryWithNoTenant.ListForParticipantAsync(FirstUserId);

        await listing.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// The important one. Knowing the conversation id and a participant's user id — both of which
    /// travel in URLs — must not be enough to read a conversation, because in Mongo the repository
    /// is the only thing standing between the two organizations.
    /// </summary>
    [Test]
    public async Task Knowing_another_organizations_conversation_id_is_not_enough_to_read_it()
    {
        SkipIfMongoIsNotReachable();

        var stolen = await CreateConversationRepository(OrganizationBId)
            .FindForParticipantAsync(_organizationAConversationId, FirstUserId);

        stolen.Should().BeNull();
    }

    [Test]
    public async Task Writes_cannot_reach_another_organizations_conversation()
    {
        SkipIfMongoIsNotReachable();

        var repositoryForB = CreateConversationRepository(OrganizationBId);

        await repositoryForB.AppendMessageAsync(
            _organizationAConversationId,
            FirstUserId,
            new ChatMessage
            {
                Id = "000000000000000000000001",
                SenderId = FirstUserId,
                Content = "smuggled",
                SentAt = DateTime.UtcNow
            });

        await repositoryForB.SetReadWatermarkAsync(_organizationAConversationId, FirstUserId, DateTime.UtcNow);

        var conversation = await CreateConversationRepository(OrganizationAId)
            .FindForParticipantAsync(_organizationAConversationId, FirstUserId);

        conversation.Should().NotBeNull();
        conversation!.Messages.Should().BeEmpty("the filter carries the organization on writes too, so the update matched nothing");
        conversation.LastReadAt.Should().BeEmpty();
    }

    /// <summary>
    /// The conversation between the same two people exists twice, once per organization, and
    /// "find by participants" resolves to the caller's copy rather than to whichever was written
    /// first.
    /// </summary>
    [Test]
    public async Task Finding_by_participants_resolves_within_the_callers_organization()
    {
        SkipIfMongoIsNotReachable();

        var participants = SortedParticipants();

        var foundByA = await CreateConversationRepository(OrganizationAId).FindByParticipantsAsync(participants);
        var foundByB = await CreateConversationRepository(OrganizationBId).FindByParticipantsAsync(participants);

        foundByA!.Id.Should().Be(_organizationAConversationId);
        foundByB!.Id.Should().Be(_organizationBConversationId);
    }

    [Test]
    public async Task An_unset_tenant_reading_conversations_raises_instead_of_returning_everything()
    {
        SkipIfMongoIsNotReachable();

        var unscopedRepository = new ChatConversationRepository(CreateMongoContext(), new TenantContext());

        var act = async () => await unscopedRepository.ListForParticipantAsync(FirstUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization context is not set.");
    }

    /// <summary>
    /// Builds the fixture's Postgres side: a throwaway database, a login role, and the schema.
    ///
    /// <para>
    /// <c>NOBYPASSRLS</c> on that role is the whole point — <c>FORCE ROW LEVEL SECURITY</c> still lets a
    /// superuser (and, without FORCE, the table owner) read everything, so isolation tested as the
    /// admin role would pass no matter what the policies said. The schema comes from the service's own
    /// migrations, run as the owner, exactly how production gets its policies; nothing here writes DDL
    /// of its own.
    /// </para>
    /// </summary>
    private static async Task SetUpPostgresAsync()
    {
        await using (var maintenanceConnection = new NpgsqlConnection(LocalSocialStoreTestSettings.AdminConnectionString()))
        {
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalSocialStoreTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalSocialStoreTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalSocialStoreTestSettings.ApplicationRoleName};");
            await ExecuteAsync(maintenanceConnection, $"CREATE DATABASE {LocalSocialStoreTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"""
                CREATE ROLE {LocalSocialStoreTestSettings.ApplicationRoleName}
                    WITH LOGIN PASSWORD '{LocalSocialStoreTestSettings.ApplicationRolePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                """);
            await ExecuteAsync(maintenanceConnection, $"""
                GRANT CONNECT ON DATABASE {LocalSocialStoreTestSettings.TestDatabaseName}
                    TO {LocalSocialStoreTestSettings.ApplicationRoleName};
                """);
        }

        await using (var migrationContext = CreateAdminContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var testDatabaseConnection = new NpgsqlConnection(
            LocalSocialStoreTestSettings.AdminConnectionString(LocalSocialStoreTestSettings.TestDatabaseName));
        await testDatabaseConnection.OpenAsync();

        await ExecuteAsync(testDatabaseConnection, $"""
            GRANT USAGE ON SCHEMA public TO {LocalSocialStoreTestSettings.ApplicationRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                TO {LocalSocialStoreTestSettings.ApplicationRoleName};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public
                TO {LocalSocialStoreTestSettings.ApplicationRoleName};
            """);

        await SeedSocialDataAsync(testDatabaseConnection);
    }

    /// <summary>
    /// Seeded as the owner, which bypasses nothing but is allowed to write rows for both
    /// organizations — the fixture needs data on both sides of the boundary it is testing.
    /// </summary>
    private static async Task SeedSocialDataAsync(NpgsqlConnection connection)
    {
        await InsertUserReplicaAsync(connection, FirstUserId, "First");
        await InsertUserReplicaAsync(connection, SecondUserId, "Second");

        await InsertThreadAsync(connection, OrganizationAThreadId, OrganizationAId);
        await InsertThreadAsync(connection, OrganizationBThreadId, OrganizationBId);

        await InsertReplyAsync(connection, OrganizationAReplyId, OrganizationAId, OrganizationAThreadId);
        await InsertReplyAsync(connection, OrganizationBReplyId, OrganizationBId, OrganizationBThreadId);

        await InsertTagAsync(connection, GlobalTagId, organizationId: null, "objections", isCurated: true);
        await InsertTagAsync(connection, OrganizationATagId, OrganizationAId, "a-only", isCurated: false);
        await InsertTagAsync(connection, OrganizationBTagId, OrganizationBId, "b-only", isCurated: false);

        await InsertFriendshipAsync(connection, OrganizationAId);
        await InsertFriendshipAsync(connection, OrganizationBId);
    }

    private static async Task InsertUserReplicaAsync(NpgsqlConnection connection, Guid userId, string displayName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "UserReplicas" ("UserId", "Email", "DisplayName", "UpdatedAt")
            VALUES (@userId, @email, @displayName, now());
            """;
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("email", $"{userId:N}@example.com");
        command.Parameters.AddWithValue("displayName", displayName);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertThreadAsync(NpgsqlConnection connection, Guid threadId, Guid organizationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DiscussThreads"
                ("Id", "OrganizationId", "AuthorId", "Title", "Body", "UpvoteCount", "ReplyCount",
                 "ViewCount", "IsPinned", "IsHot", "CreatedAt", "UpdatedAt", "LastActivityAt")
            VALUES (@id, @organizationId, @authorId, 'Title', 'Body', 0, 1, 0, false, false,
                    now(), now(), now());
            """;
        command.Parameters.AddWithValue("id", threadId);
        command.Parameters.AddWithValue("organizationId", organizationId);
        command.Parameters.AddWithValue("authorId", FirstUserId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertReplyAsync(
        NpgsqlConnection connection, Guid replyId, Guid organizationId, Guid threadId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DiscussReplies"
                ("Id", "OrganizationId", "ThreadId", "AuthorId", "Body", "UpvoteCount",
                 "IsAccepted", "CreatedAt", "UpdatedAt")
            VALUES (@id, @organizationId, @threadId, @authorId, 'Body', 0, false, now(), now());
            """;
        command.Parameters.AddWithValue("id", replyId);
        command.Parameters.AddWithValue("organizationId", organizationId);
        command.Parameters.AddWithValue("threadId", threadId);
        command.Parameters.AddWithValue("authorId", SecondUserId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertTagAsync(
        NpgsqlConnection connection, Guid tagId, Guid? organizationId, string slug, bool isCurated)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DiscussTags" ("Id", "OrganizationId", "Slug", "Name", "IsCurated", "CreatedAt")
            VALUES (@id, @organizationId, @slug, @slug, @isCurated, now());
            """;
        command.Parameters.AddWithValue("id", tagId);
        command.Parameters.AddWithValue("organizationId", (object?)organizationId ?? DBNull.Value);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("isCurated", isCurated);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertFriendshipAsync(NpgsqlConnection connection, Guid organizationId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Friendships"
                ("Id", "OrganizationId", "RequesterId", "AddresseeId", "Status", "CreatedAt", "AcceptedAt")
            VALUES (@id, @organizationId, @requesterId, @addresseeId, 1, now(), now());
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("organizationId", organizationId);
        command.Parameters.AddWithValue("requesterId", FirstUserId);
        command.Parameters.AddWithValue("addresseeId", SecondUserId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Builds the fixture's Mongo side: one conversation between the same two people per organization.
    ///
    /// <para>
    /// Inserted through the repository on purpose. Hand-written documents would let every isolation
    /// assertion above keep passing even if the repository stopped stamping the organization on write,
    /// which is the regression most worth catching here.
    /// </para>
    /// </summary>
    private async Task SetUpMongoAsync()
    {
        await new MongoClient(LocalSocialStoreTestSettings.MongoConnectionString())
            .DropDatabaseAsync(LocalSocialStoreTestSettings.MongoDatabaseName);

        var organizationAConversation = new ChatConversation
        {
            ParticipantIds = SortedParticipants(),
            CreatedAt = DateTime.UtcNow
        };
        await CreateConversationRepository(OrganizationAId).InsertAsync(organizationAConversation);
        _organizationAConversationId = organizationAConversation.Id;

        var organizationBConversation = new ChatConversation
        {
            ParticipantIds = SortedParticipants(),
            CreatedAt = DateTime.UtcNow
        };
        await CreateConversationRepository(OrganizationBId).InsertAsync(organizationBConversation);
        _organizationBConversationId = organizationBConversation.Id;
    }

    private static SocialDbContext CreateAdminContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();

        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseNpgsql(LocalSocialStoreTestSettings.AdminConnectionString(LocalSocialStoreTestSettings.TestDatabaseName))
            .Options;

        return new SocialDbContext(options, tenantContext);
    }

    private static SocialDbContext CreateApplicationContext(Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseNpgsql(LocalSocialStoreTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenantContext),
                new TenantConnectionInterceptor(tenantContext))
            .Options;

        return new SocialDbContext(options, tenantContext);
    }

    private static MongoDbContext CreateMongoContext()
        => new(
            new MongoClient(LocalSocialStoreTestSettings.MongoConnectionString()),
            Options.Create(new MongoConfiguration { DatabaseName = LocalSocialStoreTestSettings.MongoDatabaseName }));

    private static IChatConversationRepository CreateConversationRepository(Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        return new ChatConversationRepository(CreateMongoContext(), tenantContext);
    }

    /// <summary>
    /// The repository in the third tenant mode: validated platform staff, no organization. Mongo has
    /// no policies, so this class is the entire boundary — and therefore the entire widening too.
    /// </summary>
    private static IChatConversationRepository CreatePlatformStaffConversationRepository()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterPlatformMode();

        return new ChatConversationRepository(CreateMongoContext(), tenantContext);
    }

    private static List<Guid> SortedParticipants()
    {
        var participants = new List<Guid> { FirstUserId, SecondUserId };
        participants.Sort();
        return participants;
    }

    private static async Task<List<Guid>> ReadVisibleThreadIdsAsync(Guid organizationId)
    {
        await using var context = CreateApplicationContext(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var threadIds = await context.DiscussThreads.Select(thread => thread.Id).ToListAsync();

        await transaction.CommitAsync();
        return threadIds;
    }

    private static async Task<List<Guid>> ReadVisibleTagIdsAsync(Guid organizationId)
    {
        await using var context = CreateApplicationContext(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var tagIds = await context.DiscussTags.Select(tag => tag.Id).ToListAsync();

        await transaction.CommitAsync();
        return tagIds;
    }

    private static async Task<int> ReadFriendshipCountAsync(Guid organizationId)
    {
        await using var context = CreateApplicationContext(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var count = await context.Friendships.CountAsync();

        await transaction.CommitAsync();
        return count;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private void SkipIfPostgresIsNotReachable()
    {
        if (!_isPostgresReachable)
        {
            Assert.Ignore("No local Postgres on the dev-infra port; skipping the social-service RLS isolation checks.");
        }
    }

    private void SkipIfMongoIsNotReachable()
    {
        if (!_isMongoReachable)
        {
            Assert.Ignore("No local Mongo on the dev-infra port; skipping the chat-conversation isolation checks.");
        }
    }
}
