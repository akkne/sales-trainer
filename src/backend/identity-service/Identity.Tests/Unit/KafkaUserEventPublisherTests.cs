using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Identity.Eventing;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class KafkaUserEventPublisherTests
{
    [Test]
    public async Task PublishRegistered_MapsToCanonicalTopic_KeyedByUserId()
    {
        var inner = Substitute.For<IEventPublisher>();
        var publisher = new KafkaUserEventPublisher(inner);
        var userId = Guid.NewGuid();

        await publisher.PublishRegisteredAsync(new UserRegisteredEvent(userId, "a@b.com", "Alice", null));

        await inner.Received(1).PublishAsync(
            Topics.UserRegistered,
            userId.ToString(),
            Topics.UserRegistered,
            Arg.Any<UserRegisteredEvent>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishAvatarChanged_MapsToAvatarChangedTopic()
    {
        var inner = Substitute.For<IEventPublisher>();
        var publisher = new KafkaUserEventPublisher(inner);
        var userId = Guid.NewGuid();

        await publisher.PublishAvatarChangedAsync(new UserAvatarChangedEvent(userId, "k"));

        await inner.Received(1).PublishAsync(
            Topics.UserAvatarChanged,
            userId.ToString(),
            Topics.UserAvatarChanged,
            Arg.Any<UserAvatarChangedEvent>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
