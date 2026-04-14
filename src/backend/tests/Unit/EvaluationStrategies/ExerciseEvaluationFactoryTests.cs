using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class ExerciseEvaluationFactoryTests
{
    private ExerciseEvaluationFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new ExerciseEvaluationFactory([
            new ChooseOptionEvaluationStrategy(),
            new FillBlankEvaluationStrategy()
        ]);
    }

    [Test]
    public void GetStrategyForExerciseType_KnownType_ReturnsCorrectStrategy()
    {
        var strategy = _factory.GetStrategyForExerciseType(ExerciseTypes.ChooseOption);
        strategy.Should().BeOfType<ChooseOptionEvaluationStrategy>();
    }

    [Test]
    public void GetStrategyForExerciseType_FillBlank_ReturnsFillBlankStrategy()
    {
        var strategy = _factory.GetStrategyForExerciseType(ExerciseTypes.FillBlank);
        strategy.Should().BeOfType<FillBlankEvaluationStrategy>();
    }

    [Test]
    public void GetStrategyForExerciseType_UnknownType_ThrowsNotSupportedException()
    {
        var act = () => _factory.GetStrategyForExerciseType("unknown_type");
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*unknown_type*");
    }
}
