using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Social.Eventing;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Tests.Helpers;

namespace Sellevate.Social.Tests.Unit;

[TestFixture]
public sealed class UserReplicaConsumerTests
{
    private SocialDbContext _databaseContext = null!;
    private IServiceProvider _scopedServiceProvider = null!;
    private UserReplicaConsumer _consumer = null!;

    private static readonly Guid UserId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestSocialDatabaseFactory.CreateInMemory();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_databaseContext);
        _scopedServiceProvider = serviceCollection.BuildServiceProvider();

        var settings = Options.Create(new KafkaSettings
        {
            BootstrapServers = "localhost:9092",
            ConsumerGroupId = "social-service"
        });

        _consumer = new UserReplicaConsumer(
            settings,
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IIdempotencyStore>(),
            NullLogger<UserReplicaConsumer>.Instance);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task UserRegistered_seeds_replica()
    {
        var envelope = EventEnvelope.Create(
            Topics.UserRegistered,
            new UserRegisteredEvent(UserId, "user@example.com", "New User", "avatar-key"));

        await InvokeHandleAsync(envelope);

        var replica = await _databaseContext.UserReplicas.FindAsync(UserId);
        replica.Should().NotBeNull();
        replica!.DisplayName.Should().Be("New User");
        replica.Email.Should().Be("user@example.com");
    }

    [Test]
    public async Task UserRegistered_twice_is_idempotent_on_the_replica_row()
    {
        var first = EventEnvelope.Create(
            Topics.UserRegistered,
            new UserRegisteredEvent(UserId, "user@example.com", "First", null));
        var second = EventEnvelope.Create(
            Topics.UserRegistered,
            new UserRegisteredEvent(UserId, "user@example.com", "Second", null));

        await InvokeHandleAsync(first);
        await InvokeHandleAsync(second);

        _databaseContext.UserReplicas.Should().HaveCount(1);
        var replica = await _databaseContext.UserReplicas.FindAsync(UserId);
        replica!.DisplayName.Should().Be("Second");
    }

    [Test]
    public async Task UserUpdated_updates_existing_replica()
    {
        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, UserId, "Old Name");

        var envelope = EventEnvelope.Create(
            Topics.UserUpdated,
            new UserUpdatedEvent(UserId, "Updated Name", "new-avatar"));

        await InvokeHandleAsync(envelope);

        var replica = await _databaseContext.UserReplicas.FindAsync(UserId);
        replica!.DisplayName.Should().Be("Updated Name");
        replica.AvatarKey.Should().Be("new-avatar");
    }

    [Test]
    public async Task UserDeleted_removes_replica()
    {
        await TestSocialDatabaseFactory.SeedUserAsync(_databaseContext, UserId, "To Delete");

        var envelope = EventEnvelope.Create(
            Topics.UserDeleted,
            new UserDeletedEvent(UserId));

        await InvokeHandleAsync(envelope);

        _databaseContext.UserReplicas.Should().BeEmpty();
    }

    private async Task InvokeHandleAsync(EventEnvelope envelope)
    {
        var handleMethod = typeof(UserReplicaConsumer).GetMethod(
            "HandleAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        await (Task)handleMethod.Invoke(
            _consumer, [envelope, _scopedServiceProvider, CancellationToken.None])!;
    }
}
