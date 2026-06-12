using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Avatars;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class DefaultAvatarIndexResolverTests
{
    private const int CatalogSize = 6;

    // Known-value test: computed manually from the first 4 bytes of the Guid
    // Guid "a1b2c3d4-0000-0000-0000-000000000000"
    //   bytes (little-endian layout): d4 c3 b2 a1 ...
    //   value = 0xd4 | (0xc3 << 8) | (0xb2 << 16) | (0xa1 << 24)
    //         = 0xa1b2c3d4 = 2712847316u
    //   2712847316 % 6 = 2
    [Test]
    public void Resolve_KnownGuid_ReturnsExpectedIndex()
    {
        var guid = new Guid("a1b2c3d4-0000-0000-0000-000000000000");

        var result = DefaultAvatarIndexResolver.Resolve(guid, CatalogSize);

        result.Should().Be(2);
    }

    [Test]
    public void Resolve_SameGuid_ReturnsSameIndex()
    {
        var guid = Guid.NewGuid();

        var first = DefaultAvatarIndexResolver.Resolve(guid, CatalogSize);
        var second = DefaultAvatarIndexResolver.Resolve(guid, CatalogSize);

        first.Should().Be(second);
    }

    [Test]
    public void Resolve_AllResultsWithinRange()
    {
        for (var i = 0; i < 1000; i++)
        {
            var result = DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), CatalogSize);
            result.Should().BeGreaterThanOrEqualTo(0)
                .And.BeLessThan(CatalogSize);
        }
    }

    [Test]
    public void Resolve_SpreadAcrossAllIndices()
    {
        var counts = new int[CatalogSize];
        for (var i = 0; i < 10_000; i++)
        {
            var idx = DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), CatalogSize);
            counts[idx]++;
        }

        // Each bucket should appear at least once
        foreach (var count in counts)
            count.Should().BePositive();
    }

    [Test]
    public void Resolve_CatalogSizeZero_ThrowsArgumentOutOfRangeException()
    {
        var act = () => DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Resolve_CatalogSizeNegative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Resolve_CatalogSizeOne_AlwaysReturnsZero()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), 1);
            result.Should().Be(0);
        }
    }
}
