using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Endpoints;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Tests.Unit;

/// <summary>
/// Q-4 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). Until this endpoint existed the two switches on
/// <c>/settings</c> lived only in the browser, so nothing on the server could honour them.
///
/// <para>
/// Two things are worth pinning here and both are contracts other code depends on rather than
/// implementation detail. First, the <em>defaults</em>: reminders on, product updates off — the same
/// pair the browser store used, so nobody's effective settings changed on the deploy that introduced
/// this. Second, <c>isDefault</c>: the frontend's one-shot migration of the old browser values reads
/// it to tell "nobody ever set these" from "someone set them and picked the defaults", and if it
/// ever reported true after a save, a saved preference would be overwritten by whatever a stale
/// <c>localStorage</c> still held.
/// </para>
/// </summary>
[TestFixture]
public sealed class NotificationPreferencesTests
{
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static NotificationPreferencesController CreateController(
        InMemoryNotificationPreferencesStore store, Guid? userId = null) =>
        new(store)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Caller(userId ?? UserId) },
            },
        };

    private static ClaimsPrincipal Caller(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static NotificationPreferencesDto? Body(ActionResult<NotificationPreferencesDto> response) =>
        (response.Result as OkObjectResult)?.Value as NotificationPreferencesDto;

    [Test]
    public async Task Defaults_are_reminders_on_and_product_updates_off()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = CreateController(store);

        var body = Body(await controller.GetPreferences(CancellationToken.None));

        body.Should().NotBeNull();
        body!.PracticeReminders.Should().BeTrue();
        body.ProductUpdates.Should().BeFalse();
    }

    [Test]
    public async Task An_unset_preference_reads_as_default()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = CreateController(store);

        var body = Body(await controller.GetPreferences(CancellationToken.None));

        body!.IsDefault.Should().BeTrue();
    }

    [Test]
    public async Task Reading_does_not_write_a_row()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = CreateController(store);

        await controller.GetPreferences(CancellationToken.None);

        // A GET that materialised the defaults would make every user look like they had chosen them
        // and would kill the migration path before it ran once.
        store.SaveCount.Should().Be(0);
        (await store.GetAsync(UserId)).Should().BeNull();
    }

    [Test]
    public async Task Saving_the_defaults_still_stops_reading_as_default()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = CreateController(store);

        var saved = Body(await controller.UpdatePreferences(
            new UpdateNotificationPreferencesRequest(PracticeReminders: true, ProductUpdates: false),
            CancellationToken.None));

        // Same values as the defaults, but deliberately chosen — this is the case the flag exists for.
        saved!.PracticeReminders.Should().BeTrue();
        saved.ProductUpdates.Should().BeFalse();
        saved.IsDefault.Should().BeFalse();

        var reread = Body(await controller.GetPreferences(CancellationToken.None));
        reread!.IsDefault.Should().BeFalse();
    }

    [Test]
    public async Task A_saved_preference_is_returned_on_the_next_read()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = CreateController(store);

        await controller.UpdatePreferences(
            new UpdateNotificationPreferencesRequest(PracticeReminders: false, ProductUpdates: true),
            CancellationToken.None);

        var body = Body(await controller.GetPreferences(CancellationToken.None));

        body!.PracticeReminders.Should().BeFalse();
        body.ProductUpdates.Should().BeTrue();
    }

    [Test]
    public async Task One_persons_preference_is_not_another_persons()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var otherUserId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        await CreateController(store).UpdatePreferences(
            new UpdateNotificationPreferencesRequest(PracticeReminders: false, ProductUpdates: true),
            CancellationToken.None);

        var otherBody = Body(await CreateController(store, otherUserId)
            .GetPreferences(CancellationToken.None));

        otherBody!.IsDefault.Should().BeTrue();
        otherBody.PracticeReminders.Should().BeTrue();
        otherBody.ProductUpdates.Should().BeFalse();
    }

    [Test]
    public async Task A_token_without_a_subject_is_refused_rather_than_served_defaults()
    {
        var store = new InMemoryNotificationPreferencesStore();
        var controller = new NotificationPreferencesController(store)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Anonymous() },
            },
        };

        (await controller.GetPreferences(CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
        (await controller.UpdatePreferences(
            new UpdateNotificationPreferencesRequest(true, true), CancellationToken.None)).Result
            .Should().BeOfType<UnauthorizedResult>();
    }

    [Test]
    public async Task Deleting_a_user_drops_their_preference()
    {
        var store = new InMemoryNotificationPreferencesStore();
        await CreateController(store).UpdatePreferences(
            new UpdateNotificationPreferencesRequest(PracticeReminders: false, ProductUpdates: true),
            CancellationToken.None);

        await store.RemoveAsync(UserId);

        // Not merely "reads as default": the row is gone, so a reused id cannot inherit an answer
        // somebody else gave.
        (await store.GetAsync(UserId)).Should().BeNull();
    }

    /// <summary>
    /// The key shape is the whole of the cross-organization guarantee here — the service has no
    /// database and no RLS to fall back on, exactly as <see cref="NotificationTenancyTests"/> says
    /// about the inbox keys. This one has to *not* carry the prefix those do.
    /// </summary>
    [Test]
    public void Preference_keys_are_per_user_and_carry_no_organization_prefix()
    {
        var key = RedisKeys.NotificationPreferences(UserId);

        key.Should().Be($"notifications:preferences:{UserId:N}");
        key.Should().NotStartWith("org:");
    }
}
