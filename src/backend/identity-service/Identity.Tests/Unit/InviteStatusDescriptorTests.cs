using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Membership.Models;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// The status shown next to every invite in the organization's people screen (Phase 40.20).
///
/// <para>
/// It is derived from three nullable columns and a clock, which is exactly the shape that grows a
/// second, disagreeing implementation the first time somebody needs it on another screen. These
/// tests pin the precedence so that copy cannot be written by accident: a recorded fact always beats
/// the clock.
/// </para>
/// </summary>
[TestFixture]
public sealed class InviteStatusDescriptorTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void An_untouched_invite_in_date_is_pending()
        => InviteStatusDescriptor.Describe(Invitation(expiresAt: Now.AddDays(3)), Now)
            .Should().Be(InviteStatusDescriptor.Pending);

    [Test]
    public void An_untouched_invite_past_its_expiry_is_expired()
        => InviteStatusDescriptor.Describe(Invitation(expiresAt: Now.AddSeconds(-1)), Now)
            .Should().Be(InviteStatusDescriptor.Expired);

    [Test]
    public void Expiry_is_inclusive_so_the_exact_moment_counts_as_expired()
        => InviteStatusDescriptor.Describe(Invitation(expiresAt: Now), Now)
            .Should().Be(InviteStatusDescriptor.Expired);

    [Test]
    public void Acceptance_outranks_the_clock()
        => InviteStatusDescriptor.Describe(
                Invitation(expiresAt: Now.AddDays(-30), acceptedAt: Now.AddDays(-31)), Now)
            .Should().Be(InviteStatusDescriptor.Accepted);

    [Test]
    public void Revocation_outranks_the_clock()
        => InviteStatusDescriptor.Describe(
                Invitation(expiresAt: Now.AddDays(-30), revokedAt: Now.AddDays(-31)), Now)
            .Should().Be(InviteStatusDescriptor.Revoked);

    /// <summary>
    /// Revoking an already-accepted invite is not a state the flow produces — <c>RevokeAsync</c>
    /// refuses one — but if a row ever carries both, "accepted" is the truthful answer: the person is
    /// already inside, and showing "revoked" would suggest they are not.
    /// </summary>
    [Test]
    public void Acceptance_wins_over_revocation_if_a_row_somehow_carries_both()
        => InviteStatusDescriptor.Describe(
                Invitation(expiresAt: Now.AddDays(1), acceptedAt: Now, revokedAt: Now), Now)
            .Should().Be(InviteStatusDescriptor.Accepted);

    private static Invite Invitation(
        DateTime expiresAt, DateTime? acceptedAt = null, DateTime? revokedAt = null) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Email = "invitee@example.com",
            Role = OrgRole.Manager,
            TokenHash = "hash",
            ExpiresAt = expiresAt,
            AcceptedAt = acceptedAt,
            RevokedAt = revokedAt,
            CreatedAt = Now.AddDays(-1),
        };
}
