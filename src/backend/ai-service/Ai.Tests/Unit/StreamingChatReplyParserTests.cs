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
    public void Complete_ReadsEndCallReason_WhenCharacterHangsUp()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "Так со мной разговаривать не нужно. Всего доброго.", "endCall": true, "endCallReason": "оскорбления"}""");
        var result = parser.Complete();

        result.EndCall.Should().BeTrue();
        result.EndCallReason.Should().Be("оскорбления");
    }

    [Test]
    public void Complete_EndCallReasonIsNull_WhenModelReturnsNull()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "Слушаю вас.", "endCall": false, "endCallReason": null}""");
        var result = parser.Complete();

        result.EndCall.Should().BeFalse();
        result.EndCallReason.Should().BeNull();
    }

    [Test]
    public void Complete_ForcesEndCall_WhenReplyIsFarewellButFlagIsFalse()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "Так со мной разговаривать не нужно. Всего доброго.", "endCall": false, "endCallReason": null}""");
        var result = parser.Complete();

        result.EndCall.Should().BeTrue();
        result.EndCallReason.Should().Be("farewell");
    }

    [Test]
    public void Complete_KeepsModelReason_WhenFarewellAlsoHasExplicitEndCall()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "Всего доброго.", "endCall": true, "endCallReason": "оскорбления"}""");
        var result = parser.Complete();

        result.EndCall.Should().BeTrue();
        result.EndCallReason.Should().Be("оскорбления");
    }

    [Test]
    public void Complete_DoesNotForceEndCall_ForOrdinaryReply()
    {
        var parser = new StreamingChatReplyParser();

        parser.Push("""{"reply": "Интересно, а что именно вы предлагаете?", "endCall": false, "endCallReason": null}""");
        var result = parser.Complete();

        result.EndCall.Should().BeFalse();
        result.EndCallReason.Should().BeNull();
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
