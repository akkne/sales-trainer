using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Eventing;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class DialogScoringWeightsProviderTests
{
    [Test]
    public void Current_DefaultsTo_EvenWeightsAndUnitMultiplier()
    {
        var provider = new DialogScoringWeightsProvider();

        provider.Current.Should().Be(new DialogScoringWeights(25, 25, 25, 25, 1.0));
    }

    [Test]
    public void Update_ReplacesTheCachedWeights()
    {
        var provider = new DialogScoringWeightsProvider();
        var updated = new DialogScoringWeights(40, 20, 20, 20, 1.5);

        provider.Update(updated);

        provider.Current.Should().Be(updated);
    }

    [Test]
    public void Update_WithNull_Throws()
    {
        var provider = new DialogScoringWeightsProvider();

        var act = () => provider.Update(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
