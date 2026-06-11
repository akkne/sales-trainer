using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Voice.Services.Implementation;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class SentenceChunkerTests
{
    private SentenceChunker _chunker = null!;

    [SetUp]
    public void SetUp()
    {
        _chunker = new SentenceChunker();
    }

    [Test]
    public void TryExtractSentence_EmptyBuffer_ReturnsFalse()
    {
        _chunker.TryExtractSentence(out _).Should().BeFalse();
    }

    [Test]
    public void FirstChunk_ShorterThanMinimum_NotExtracted()
    {
        _chunker.Append("Алло, да.");

        _chunker.TryExtractSentence(out _).Should().BeFalse();
    }

    [Test]
    public void FirstChunk_SplitsOnCommaFollowedByWhitespace()
    {
        _chunker.Append("Добрый день коллеги, чем могу помочь?");

        _chunker.TryExtractSentence(out var sentence).Should().BeTrue();
        sentence.Should().Be("Добрый день коллеги,");
    }

    [Test]
    public void FirstChunk_SplitsOnSentenceEnder()
    {
        _chunker.Append("Слушаю вас внимательно. Что вы хотели?");

        _chunker.TryExtractSentence(out var sentence).Should().BeTrue();
        sentence.Should().Be("Слушаю вас внимательно.");
    }

    [Test]
    public void FirstChunk_DoesNotSplitCommaInsideNumber()
    {
        _chunker.Append("Цена будет 1,5 миллиона рублей за объект. Это финальное предложение.");

        _chunker.TryExtractSentence(out var sentence).Should().BeTrue();
        sentence.Should().Be("Цена будет 1,5 миллиона рублей за объект.");
    }

    [Test]
    public void SubsequentChunks_IgnoreClauseDelimiters()
    {
        _chunker.Append("Добрый день коллеги, во-первых, во-вторых, в-третьих, без конца предложения");
        _chunker.TryExtractSentence(out _).Should().BeTrue();

        // The rest contains only commas — no sentence ender, so nothing more is extracted.
        _chunker.TryExtractSentence(out _).Should().BeFalse();

        _chunker.Append(" и вот теперь точка.");
        _chunker.TryExtractSentence(out var sentence).Should().BeTrue();
        sentence.Should().EndWith("и вот теперь точка.");
    }

    [Test]
    public void SubsequentChunks_RequireLongerMinimumLength()
    {
        _chunker.Append("Здравствуйте, Иван. Да. Конечно же обсудим всё.");
        _chunker.TryExtractSentence(out var first).Should().BeTrue();
        first.Should().Be("Здравствуйте,");

        // "Иван. Да. Конечно ж" is below the 20-char minimum before the next ender;
        // the split lands on the first ender at/after position 20.
        _chunker.TryExtractSentence(out var second).Should().BeTrue();
        second.Should().Be(" Иван. Да. Конечно же обсудим всё.");
    }

    [Test]
    public void StreamedAppends_ExtractAcrossDeltaBoundaries()
    {
        _chunker.Append("Здравству");
        _chunker.TryExtractSentence(out _).Should().BeFalse();

        _chunker.Append("йте, это компания Ромашка.");
        _chunker.TryExtractSentence(out var sentence).Should().BeTrue();
        sentence.Should().Be("Здравствуйте,");
    }

    [Test]
    public void DrainRemaining_ReturnsTailAndClearsBuffer()
    {
        _chunker.Append("Хорошо, договорились тогда. небольшой хвост");
        _chunker.TryExtractSentence(out _).Should().BeTrue();

        _chunker.DrainRemaining().Should().Be(" небольшой хвост");
        _chunker.DrainRemaining().Should().BeEmpty();
    }

    [Test]
    public void Replace_OverwritesBufferedText()
    {
        _chunker.Append("частично распарсенный мусор");
        _chunker.Replace("чистый ответ модели");

        _chunker.DrainRemaining().Should().Be("чистый ответ модели");
    }
}
