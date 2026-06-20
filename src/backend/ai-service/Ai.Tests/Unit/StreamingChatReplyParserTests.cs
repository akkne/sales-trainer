using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog.Services.Implementation;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class StreamingChatReplyParserTests
{
    [Test]
    public void Push_ExtractsReplyText_FromStructuredJson()
    {
        var parser = new StreamingChatReplyParser();

        var emitted = parser.Push("""{"reply": "Здравствуйте, чем могу помочь?", "endCall": false}""");

        emitted.Should().Be("Здравствуйте, чем могу помочь?");
        var result = parser.Complete();
        result.Reply.Should().Be("Здравствуйте, чем могу помочь?");
        result.EndCall.Should().BeFalse();
        result.UsedFallback.Should().BeFalse();
    }

    [Test]
    public void Complete_ReadsEndCallTrue()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "До свидания.", "endCall": true}""");
        var result = parser.Complete();

        result.EndCall.Should().BeTrue();
    }

    [Test]
    public void Complete_FallsBackToPlainText_WhenModelIgnoresContract()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("Просто текст без JSON.");
        var result = parser.Complete();

        result.UsedFallback.Should().BeTrue();
        result.Reply.Should().Be("Просто текст без JSON.");
    }

    [Test]
    public void Push_StreamedInChunks_ReassemblesReply()
    {
        var parser = new StreamingChatReplyParser();

        var emitted = parser.Push("{\"reply\": \"При") + parser.Push("вет\"}");

        emitted.Should().Be("Привет");
    }
}
