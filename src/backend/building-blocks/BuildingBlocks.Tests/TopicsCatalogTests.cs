using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Tests;

/// <summary>
/// Guards the reflected <see cref="Topics.All"/> set that the startup
/// <c>KafkaTopicProvisioner</c> uses to create topics — a topic missing from this set would
/// never be provisioned and (on an auto-create-disabled broker) would silently break delivery.
/// </summary>
[TestFixture]
public sealed class TopicsCatalogTests
{
    [Test]
    public void All_ContainsEveryDeclaredBaseTopic()
    {
        Topics.All.Should().Contain(new[]
        {
            Topics.UserRegistered,
            Topics.FriendRequestReceived,
            Topics.FriendRequestAccepted,
            Topics.ChatMessageSent,
            Topics.ChatMessageRead,
            Topics.DiscussReplyCreated,
            Topics.AchievementUnlocked,
            Topics.StreakMilestone,
            Topics.CompanyFollowUpDue,
        });
    }

    [Test]
    public void All_ExcludesTheDeadLetterSuffixHelper()
    {
        Topics.All.Should().NotContain(Topics.DeadLetterSuffix);
        Topics.All.Should().OnlyContain(topic => !topic.StartsWith('.'));
    }

    [Test]
    public void All_HasNoDuplicates()
    {
        Topics.All.Should().OnlyHaveUniqueItems();
    }
}
