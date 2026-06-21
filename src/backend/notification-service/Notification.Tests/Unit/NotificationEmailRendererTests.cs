using FluentAssertions;
using NUnit.Framework;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Emails.Templates;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Tests.Unit;

[TestFixture]
public class NotificationEmailRendererTests
{
    private const string FrontendBaseUrl = "https://app.sellevate.test";

    private static NotificationEmailRenderer CreateRenderer() =>
        new(
            new INotificationEmailTemplate[]
            {
                new FriendRequestEmailTemplate(),
                new FriendRequestAcceptedEmailTemplate(),
                new ChatMessageEmailTemplate(),
                new DiscussReplyEmailTemplate(),
                new LeagueUpdatedEmailTemplate(),
                new WelcomeEmailTemplate(),
            },
            new GenericNotificationEmailTemplate(),
            FrontendBaseUrl);

    private static NotificationEmailContext Context(
        NotificationType type, string body = "Hello body", string? actionUrl = "/friends/chat/abc") =>
        new("Alex", type, "Some title", body, actionUrl);

    [Test]
    public void Render_ChatMessage_UsesChatTemplateAndAbsoluteUrl()
    {
        var content = CreateRenderer().Render(Context(NotificationType.ChatMessageReceived));

        content.Subject.Should().Be("You have a new message on Sellevate");
        content.HtmlBody.Should().Contain("Hi Alex,");
        content.HtmlBody.Should().Contain("Hello body");
        content.HtmlBody.Should().Contain("View message");
        content.HtmlBody.Should().Contain($"{FrontendBaseUrl}/friends/chat/abc");
        content.TextBody.Should().Contain("Hello body");
    }

    [Test]
    public void Render_FriendRequest_UsesFriendRequestTemplate()
    {
        var content = CreateRenderer().Render(
            Context(NotificationType.FriendRequestReceived, actionUrl: "/friends?tab=requests"));

        content.Subject.Should().Be("You have a new friend request");
        content.HtmlBody.Should().Contain("New friend request");
        content.HtmlBody.Should().Contain("View request");
        content.HtmlBody.Should().Contain($"{FrontendBaseUrl}/friends?tab=requests");
    }

    [Test]
    public void Render_FriendRequestAccepted_UsesAcceptedTemplate()
    {
        var content = CreateRenderer().Render(
            Context(NotificationType.FriendRequestAccepted, actionUrl: "/friends/abc"));

        content.Subject.Should().Be("Your friend request was accepted");
        content.HtmlBody.Should().Contain("Friend request accepted");
        content.HtmlBody.Should().Contain("View profile");
        content.HtmlBody.Should().Contain($"{FrontendBaseUrl}/friends/abc");
    }

    [Test]
    public void Render_DiscussReply_UsesDiscussTemplate()
    {
        var content = CreateRenderer().Render(Context(NotificationType.DiscussReplyReceived, actionUrl: "/discuss/42"));

        content.Subject.Should().Be("New reply to your discussion");
        content.HtmlBody.Should().Contain("View discussion");
        content.HtmlBody.Should().Contain($"{FrontendBaseUrl}/discuss/42");
    }

    [Test]
    public void Render_LeagueUpdated_UsesLeagueTemplate()
    {
        var content = CreateRenderer().Render(Context(NotificationType.LeagueUpdated, actionUrl: "/league"));

        content.Subject.Should().Be("Your Sellevate league was updated");
        content.HtmlBody.Should().Contain("View league");
    }

    [Test]
    public void Render_Welcome_UsesWelcomeTemplate()
    {
        var content = CreateRenderer().Render(Context(NotificationType.UserWelcome, actionUrl: "/"));

        content.Subject.Should().Be("Welcome to Sellevate");
        content.HtmlBody.Should().Contain("Welcome aboard");
        content.HtmlBody.Should().Contain("Hi Alex,");
        content.HtmlBody.Should().Contain("Start training");
        content.HtmlBody.Should().Contain($"{FrontendBaseUrl}/");
    }

    [Test]
    public void Render_UnmappedType_FallsBackToGenericTemplateUsingTitle()
    {
        // AchievementUnlocked has no dedicated email template.
        var content = CreateRenderer().Render(Context(NotificationType.AchievementUnlocked));

        content.Subject.Should().Be("Some title");
        content.HtmlBody.Should().Contain("Some title");
        content.HtmlBody.Should().Contain("Hello body");
    }

    [Test]
    public void Render_EncodesUntrustedBody()
    {
        var content = CreateRenderer().Render(
            Context(NotificationType.DiscussReplyReceived, body: "<script>alert(1)</script>"));

        content.HtmlBody.Should().NotContain("<script>");
        content.HtmlBody.Should().Contain("&lt;script&gt;");
    }

    [Test]
    public void Render_NoActionUrl_OmitsButton()
    {
        var content = CreateRenderer().Render(Context(NotificationType.LeagueUpdated, actionUrl: null));

        content.HtmlBody.Should().NotContain("View league");
    }

    [Test]
    public void Render_AbsoluteActionUrl_IsLeftUnchanged()
    {
        var content = CreateRenderer().Render(
            Context(NotificationType.ChatMessageReceived, actionUrl: "https://other.example/x"));

        content.HtmlBody.Should().Contain("https://other.example/x");
    }
}
