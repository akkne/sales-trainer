using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Dialog.Services.Implementation;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class StreamingChatReplyParserTests
{
    private static (string EmittedText, ChatReplyParseResult Result) ParseInChunks(string payload, int chunkSize)
    {
        var parser = new StreamingChatReplyParser();
        var emittedText = new System.Text.StringBuilder();
        for (var offset = 0; offset < payload.Length; offset += chunkSize)
        {
            var chunk = payload.Substring(offset, Math.Min(chunkSize, payload.Length - offset));
            emittedText.Append(parser.Push(chunk));
        }
        return (emittedText.ToString(), parser.Complete());
    }

    [Test]
    public void Parses_Simple_Reply_With_EndCall_False()
    {
        var (emittedText, result) = ParseInChunks("{\"reply\": \"Привет, слушаю вас.\", \"endCall\": false}", 7);

        emittedText.Should().Be("Привет, слушаю вас.");
        result.Reply.Should().Be("Привет, слушаю вас.");
        result.EndCall.Should().BeFalse();
        result.UsedFallback.Should().BeFalse();
    }

    [Test]
    public void Parses_EndCall_True()
    {
        var (_, result) = ParseInChunks("{\"reply\": \"Такое общение неприемлемо. Всего хорошего.\", \"endCall\": true}", 3);

        result.EndCall.Should().BeTrue();
        result.Reply.Should().Be("Такое общение неприемлемо. Всего хорошего.");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(1000)]
    public void Emits_Same_Text_For_Any_Chunk_Size(int chunkSize)
    {
        const string payload = "{\"reply\":\"Добрый день! Что конкретно вы предлагаете?\",\"endCall\":false}";

        var (emittedText, result) = ParseInChunks(payload, chunkSize);

        emittedText.Should().Be("Добрый день! Что конкретно вы предлагаете?");
        result.Reply.Should().Be(emittedText);
    }

    [Test]
    public void Decodes_Escaped_Characters_Inside_Reply()
    {
        var (emittedText, result) = ParseInChunks(
            "{\"reply\": \"Он сказал: \\\"нет\\\".\\nПерезвоните позже\\t\\u0021\", \"endCall\": false}", 4);

        emittedText.Should().Be("Он сказал: \"нет\".\nПерезвоните позже\t!");
        result.Reply.Should().Be(emittedText);
    }

    [Test]
    public void Handles_Reply_Key_Split_Across_Chunk_Boundary()
    {
        var parser = new StreamingChatReplyParser();
        var emittedText = parser.Push("{\"rep") + parser.Push("ly\": \"Да") + parser.Push(", удобно.\", \"endCall\": false}");

        emittedText.Should().Be("Да, удобно.");
        parser.Complete().Reply.Should().Be("Да, удобно.");
    }

    [Test]
    public void Falls_Back_To_Plain_Text_When_Model_Ignores_Json_Contract()
    {
        var (emittedText, result) = ParseInChunks("Здравствуйте, чем могу помочь?", 6);

        emittedText.Should().BeEmpty();
        result.UsedFallback.Should().BeTrue();
        result.Reply.Should().Be("Здравствуйте, чем могу помочь?");
        result.EndCall.Should().BeFalse();
    }

    [Test]
    public void Fallback_Detects_Legacy_Dialog_End_Tag()
    {
        var (_, result) = ParseInChunks("Я уже сказал нет. До свидания.\n[DIALOG_END]", 9);

        result.UsedFallback.Should().BeTrue();
        result.Reply.Should().Be("Я уже сказал нет. До свидания.");
        result.EndCall.Should().BeTrue();
    }

    [Test]
    public void Fallback_Strips_Markdown_Code_Fence()
    {
        var (_, result) = ParseInChunks("```json\n{\"answer\": \"плохой ключ\", \"reply2\": 1}\n```", 1000);

        result.UsedFallback.Should().BeTrue();
        result.EndCall.Should().BeFalse();
    }

    [Test]
    public void Truncated_Json_Returns_Partial_Reply_Without_EndCall()
    {
        var (emittedText, result) = ParseInChunks("{\"reply\": \"Подождите, я сейчас посмо", 8);

        emittedText.Should().Be("Подождите, я сейчас посмо");
        result.Reply.Should().Be(emittedText);
        result.EndCall.Should().BeFalse();
        result.UsedFallback.Should().BeFalse();
    }

    [Test]
    public void Resolves_EndCall_From_Lenient_Match_When_Json_Is_Malformed()
    {
        var (_, result) = ParseInChunks("{\"reply\": \"До свидания.\", \"endCall\": true", 5);

        result.Reply.Should().Be("До свидания.");
        result.EndCall.Should().BeTrue();
    }

    [Test]
    public void Whitespace_Around_Colon_And_Quotes_Is_Tolerated()
    {
        var (emittedText, result) = ParseInChunks("{ \"reply\" :   \"Слушаю вас внимательно.\" , \"endCall\" : false }", 3);

        emittedText.Should().Be("Слушаю вас внимательно.");
        result.EndCall.Should().BeFalse();
    }

    [Test]
    public void Json_With_Reply_Key_Inside_Fallback_Object_Is_Recovered()
    {
        // Model answered with a different wrapper but a recoverable text field.
        var (_, result) = ParseInChunks("{\"content\": \"Добрый день, кто вы?\"}", 1000);

        result.UsedFallback.Should().BeTrue();
        result.Reply.Should().Be("Добрый день, кто вы?");
    }
}
