using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Idempotency;

namespace Sellevate.BuildingBlocks.Tests.Idempotency;

/// <summary>
/// Phase 40.11. The dedupe namespace is per organization, taken from the event envelope. Two
/// tenants sharing it would let one organization's processed event suppress another's — and a key
/// listing would tell an operator nothing about who a key belongs to.
/// </summary>
[TestFixture]
public class RedisIdempotencyStoreKeyTests
{
    private static readonly Guid EventId = new("11111111-2222-4333-8444-555555555555");
    private static readonly Guid OrganizationId = new("00000000-0000-4000-8000-000000000001");

    [Test]
    public void An_event_carrying_an_organization_gets_a_prefixed_key()
        => RedisIdempotencyStore.Key("learning", EventId, OrganizationId)
            .Should().Be($"org:{OrganizationId}:idem:learning:{EventId:N}");

    /// <summary>
    /// Platform-global events keep the historical shape on purpose: there is no tenant to mix up,
    /// and the unchanged key is what keeps events processed before 40.11 recognized as processed.
    /// </summary>
    [Test]
    public void An_event_without_an_organization_keeps_the_historical_key()
        => RedisIdempotencyStore.Key("learning", EventId, organizationId: null)
            .Should().Be($"idem:learning:{EventId:N}");

    [Test]
    public void Two_organizations_never_share_a_key_for_the_same_event()
    {
        var first = RedisIdempotencyStore.Key("learning", EventId, Guid.NewGuid());
        var second = RedisIdempotencyStore.Key("learning", EventId, Guid.NewGuid());

        first.Should().NotBe(second);
    }
}
