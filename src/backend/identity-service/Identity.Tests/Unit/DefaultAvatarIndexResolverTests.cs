using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Features.Avatars;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class DefaultAvatarIndexResolverTests
{
    [Test]
    public void Resolve_IsDeterministic_ForSameGuid()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var first = DefaultAvatarIndexResolver.Resolve(id, 6);
        var second = DefaultAvatarIndexResolver.Resolve(id, 6);

        first.Should().Be(second);
    }

    [Test]
    public void Resolve_StaysWithinCatalogBounds()
    {
        for (var i = 0; i < 200; i++)
        {
            var index = DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), 6);
            index.Should().BeInRange(0, 5);
        }
    }

    [Test]
    public void Resolve_Throws_WhenCatalogSizeNotPositive()
    {
        var act = () => DefaultAvatarIndexResolver.Resolve(Guid.NewGuid(), 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
