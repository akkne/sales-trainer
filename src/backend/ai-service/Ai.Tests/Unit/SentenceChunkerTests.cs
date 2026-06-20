using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Voice.Services.Implementation;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class SentenceChunkerTests
{
    [Test]
    public void TryExtractSentence_SplitsOnSentenceEnder()
    {
        var chunker = new SentenceChunker();
        chunker.Append("Это первое предложение. И ещё немного текста потом.");

        chunker.TryExtractSentence(out var first).Should().BeTrue();
        first.Trim().Should().Be("Это первое предложение.");
    }

    [Test]
    public void TryExtractSentence_BuffersUntilMinimumLengthReached()
    {
        var chunker = new SentenceChunker();
        chunker.Append("Да.");

        chunker.TryExtractSentence(out _).Should().BeFalse();
    }

    [Test]
    public void DrainRemaining_ReturnsBufferedTail_AndClears()
    {
        var chunker = new SentenceChunker();
        chunker.Append("Хвост без терминатора");

        chunker.DrainRemaining().Should().Be("Хвост без терминатора");
        chunker.DrainRemaining().Should().BeEmpty();
    }
}
