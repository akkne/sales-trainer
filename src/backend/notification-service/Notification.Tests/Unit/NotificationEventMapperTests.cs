using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Eventing;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Tests.Unit;

[TestFixture]
public class NotificationEventMapperTests
{
    private readonly NotificationEventMapper _mapper = new();

    [Test]
    public void Map_AchievementUnlocked_ProducesAchievementNotification()
    {
        var userId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.AchievementUnlocked,
            new AchievementUnlockedEvent(userId, "first-deal", "First Deal Closed"));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(userId);
        request.NotificationType.Should().Be(NotificationType.AchievementUnlocked);
        request.Body.Should().Be("First Deal Closed");
        request.ActionUrl.Should().Be("/profile");
        request.RelatedEntityId.Should().Be("first-deal");
    }

    [Test]
    public void Map_StreakMilestone_IncludesBonusXpWhenPresent()
    {
        var userId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.StreakMilestone,
            new StreakMilestoneEvent(userId, 7, 50));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.StreakMilestone);
        request.Body.Should().Contain("7-day");
        request.Body.Should().Contain("50");
    }

    [Test]
    public void Map_FriendRequestReceived_LinksToRequestsTab()
    {
        var recipientId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestReceived,
            new FriendRequestReceivedEvent(recipientId, "Dana", Guid.NewGuid(), Guid.NewGuid()));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(recipientId);
        request.NotificationType.Should().Be(NotificationType.FriendRequestReceived);
        request.ActionUrl.Should().Be("/friends?tab=requests");
        request.Body.Should().Contain("Dana");
        request.SendEmail.Should().BeTrue();
    }

    [Test]
    public void Map_FriendRequestAccepted_LinksToAccepterProfileWhenIdPresent()
    {
        var recipientId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestAccepted,
            new FriendRequestAcceptedEvent(recipientId, "Max", accepterId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.FriendRequestAccepted);
        request.ActionUrl.Should().Be($"/friends/{accepterId}");
        request.SendEmail.Should().BeTrue();
    }

    [Test]
    public void Map_ChatMessageSent_LinksToConversationAndTruncatesPreview()
    {
        var recipientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var longPreview = new string('a', 400);
        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "Lena", longPreview, conversationId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.NotificationType.Should().Be(NotificationType.ChatMessageReceived);
        request.ActionUrl.Should().Be($"/friends/chat/{conversationId}");
        request.Body.Length.Should().BeLessThanOrEqualTo("Lena: ".Length + 160);
    }

    [Test]
    public void Map_UnknownEventType_ReturnsNull()
    {
        var envelope = EventEnvelope.Create("some.unrelated.event", new { value = 1 });

        var request = _mapper.Map(envelope);

        request.Should().BeNull();
    }

    [Test]
    public void Map_FriendRequestReceived_WithBlankRequesterName_ReturnsNull()
    {
        var envelope = EventEnvelope.Create(
            Topics.FriendRequestReceived,
            new FriendRequestReceivedEvent(Guid.NewGuid(), "  ", null, null));

        var request = _mapper.Map(envelope);

        request.Should().BeNull();
    }

    // ── NO4a: surrogate-safe (rune-boundary) truncation ──────────────────────

    [Test]
    public void Map_ChatMessageSent_PreviewExactlyAtLimit_IsNotTruncated()
    {
        // 160 ASCII characters — must come through without an ellipsis.
        var recipientId    = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var exactPreview   = new string('x', 160);

        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "A", exactPreview, conversationId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        // Body = "A: " + 160 'x' — no ellipsis
        request!.Body.Should().EndWith("x");
        request.Body.Should().NotEndWith("…");
    }

    [Test]
    public void Map_ChatMessageSent_PreviewOverLimitWithMultibyteChar_TruncatesOnRuneBoundary()
    {
        // Build a preview where the 160th rune position is occupied by a supplementary
        // character (U+1F600 GRINNING FACE) that takes 2 UTF-16 code units.
        // A naive [..160] slice would land inside the surrogate pair and produce an
        // ill-formed string; the rune-aware truncation must stop before it.
        var recipientId    = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        // 159 ASCII chars + one emoji (2 code units) = 161 code units total, 160 runes.
        // Then append more text so the total exceeds 160 runes → truncation triggers.
        var emoji   = char.ConvertFromUtf32(0x1F600); // 2 UTF-16 code units
        var preview = new string('a', 159) + emoji + new string('b', 10); // 170 runes

        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "S", preview, conversationId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        // Must end with the ellipsis and must be a valid (well-formed) string.
        request!.Body.Should().EndWith("…");
        request.Body.IsNormalized().Should().BeTrue("truncated string must not contain unpaired surrogates");
    }

    // ── Discuss reply ────────────────────────────────────────────────────────

    [Test]
    public void Map_DiscussReplyCreated_FlagsEmailAndLinksToThread()
    {
        var threadAuthorId = Guid.NewGuid();
        var replyAuthorId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var replyId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.DiscussReplyCreated,
            new DiscussReplyCreatedEvent(
                threadAuthorId, replyAuthorId, "Pat", threadId, "How to close deals", replyId, "Try mirroring"));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(threadAuthorId);
        request.NotificationType.Should().Be(NotificationType.DiscussReplyReceived);
        request.SendEmail.Should().BeTrue();
        request.ActionUrl.Should().Be($"/discuss/{threadId}");
        request.RelatedEntityId.Should().Be(replyId.ToString());
        request.Body.Should().Contain("Pat");
        request.Body.Should().Contain("How to close deals");
    }

    [Test]
    public void Map_DiscussReplyCreated_SelfReply_ReturnsNull()
    {
        var sameUser = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.DiscussReplyCreated,
            new DiscussReplyCreatedEvent(
                sameUser, sameUser, "Me", Guid.NewGuid(), "My thread", Guid.NewGuid(), "self note"));

        _mapper.Map(envelope).Should().BeNull();
    }

    // ── League updated ───────────────────────────────────────────────────────

    [Test]
    public void Map_LeagueUpdated_Promoted_FlagsEmailWithTierInBody()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.LeagueUpdated,
            new LeagueUpdatedEvent(userId, leagueId, "silver", "gold", "promoted", 2));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.RecipientUserId.Should().Be(userId);
        request.NotificationType.Should().Be(NotificationType.LeagueUpdated);
        request.SendEmail.Should().BeTrue();
        request.ActionUrl.Should().Be("/league");
        request.RelatedEntityId.Should().Be(leagueId.ToString());
        request.Body.Should().Contain("promoted");
        request.Body.Should().Contain("Gold");
    }

    [Test]
    public void Map_LeagueUpdated_Demoted_MentionsDrop()
    {
        var envelope = EventEnvelope.Create(
            Topics.LeagueUpdated,
            new LeagueUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), "gold", "silver", "demoted", 25));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.Body.Should().Contain("Silver");
    }

    [Test]
    public void Map_ChatMessageSent_DoesNotFlagEmail()
    {
        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(Guid.NewGuid(), "Lena", "hi", Guid.NewGuid()));

        _mapper.Map(envelope)!.SendEmail.Should().BeFalse();
    }

    [Test]
    public void Map_ChatMessageSent_EmptyPreview_ProducesBodyWithSenderNameOnly()
    {
        var recipientId    = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var envelope = EventEnvelope.Create(
            Topics.ChatMessageSent,
            new ChatMessageSentEvent(recipientId, "Zara", string.Empty, conversationId));

        var request = _mapper.Map(envelope);

        request.Should().NotBeNull();
        request!.Body.Should().Be("Zara: ");
    }
}
