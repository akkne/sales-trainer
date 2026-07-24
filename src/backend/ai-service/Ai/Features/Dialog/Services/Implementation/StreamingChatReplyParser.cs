using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

internal sealed class StreamingChatReplyParser
{
    private enum ParserState
    {
        SeekingReplyKey,
        SeekingColon,
        SeekingOpeningQuote,
        InsideReplyString,
        InsideEscapeSequence,
        InsideUnicodeEscape,
        AfterReplyString
    }

    private const string ReplyKeyToken = "\"reply\"";

    private readonly StringBuilder _rawResponse = new();
    private readonly StringBuilder _decodedReply = new();
    private readonly StringBuilder _unicodeEscapeDigits = new();
    private ParserState _state = ParserState.SeekingReplyKey;
    private int _nextUnprocessedIndex;

    public bool ReplyStarted { get; private set; }
    public bool ReplyCompleted { get; private set; }

    public string Push(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            return string.Empty;

        _rawResponse.Append(delta);
        var emittedReplyText = new StringBuilder();

        while (_nextUnprocessedIndex < _rawResponse.Length)
        {
            if (_state == ParserState.SeekingReplyKey)
            {
                if (!TryLocateReplyKey())
                    break;
                continue;
            }

            var currentCharacter = _rawResponse[_nextUnprocessedIndex];

            switch (_state)
            {
                case ParserState.SeekingColon:
                    _nextUnprocessedIndex++;
                    if (currentCharacter == ':')
                        _state = ParserState.SeekingOpeningQuote;
                    else if (!char.IsWhiteSpace(currentCharacter))
                        _state = ParserState.AfterReplyString;
                    break;

                case ParserState.SeekingOpeningQuote:
                    _nextUnprocessedIndex++;
                    if (currentCharacter == '"')
                    {
                        _state = ParserState.InsideReplyString;
                        ReplyStarted = true;
                    }
                    else if (!char.IsWhiteSpace(currentCharacter))
                    {
                        _state = ParserState.AfterReplyString;
                    }
                    break;

                case ParserState.InsideReplyString:
                    _nextUnprocessedIndex++;
                    if (currentCharacter == '"')
                    {
                        ReplyCompleted = true;
                        _state = ParserState.AfterReplyString;
                    }
                    else if (currentCharacter == '\\')
                    {
                        _state = ParserState.InsideEscapeSequence;
                    }
                    else
                    {
                        _decodedReply.Append(currentCharacter);
                        emittedReplyText.Append(currentCharacter);
                    }
                    break;

                case ParserState.InsideEscapeSequence:
                    _nextUnprocessedIndex++;
                    if (currentCharacter == 'u')
                    {
                        _unicodeEscapeDigits.Clear();
                        _state = ParserState.InsideUnicodeEscape;
                    }
                    else
                    {
                        var decodedCharacter = currentCharacter switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            'r' => '\r',
                            'b' => '\b',
                            'f' => '\f',
                            _ => currentCharacter
                        };
                        _decodedReply.Append(decodedCharacter);
                        emittedReplyText.Append(decodedCharacter);
                        _state = ParserState.InsideReplyString;
                    }
                    break;

                case ParserState.InsideUnicodeEscape:
                    _nextUnprocessedIndex++;
                    _unicodeEscapeDigits.Append(currentCharacter);
                    if (_unicodeEscapeDigits.Length == 4)
                    {
                        if (ushort.TryParse(_unicodeEscapeDigits.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
                        {
                            var decodedCharacter = (char)codePoint;
                            _decodedReply.Append(decodedCharacter);
                            emittedReplyText.Append(decodedCharacter);
                        }
                        _state = ParserState.InsideReplyString;
                    }
                    break;

                case ParserState.AfterReplyString:
                    _nextUnprocessedIndex = _rawResponse.Length;
                    break;
            }
        }

        return emittedReplyText.ToString();
    }

    public ChatReplyParseResult Complete()
    {
        var rawText = _rawResponse.ToString();

        if (ReplyStarted)
        {
            var reply = _decodedReply.ToString().Trim();
            return BuildResult(reply, ResolveEndCallFlag(rawText), ResolveEndCallReason(rawText), usedFallback: false);
        }

        var fallbackReply = ExtractFallbackReply(rawText);
        var fallbackEndCall = ResolveEndCallFlag(rawText) || rawText.Contains("[DIALOG_END]", StringComparison.Ordinal);
        return BuildResult(fallbackReply, fallbackEndCall, ResolveEndCallReason(rawText), usedFallback: true);
    }

    // Safety net: models sometimes voice a goodbye in the reply but leave endCall=false, so the call never
    // hangs up (observed with abusive callers). A persona farewell always terminates the call, so force it.
    private static ChatReplyParseResult BuildResult(string reply, bool endCall, string? endCallReason, bool usedFallback)
    {
        if (!endCall && LooksLikeFarewell(reply))
        {
            endCall = true;
            endCallReason ??= "farewell";
        }

        return new ChatReplyParseResult(reply, endCall, usedFallback, endCallReason);
    }

    private static readonly string[] FarewellMarkers =
    {
        "всего доброго",
        "всего хорошего",
        "до свидания",
        "всего наилучшего",
        "кладу трубку",
        "разговор окончен",
        "на этом закончим",
        "на этом всё",
        "разговор закончен",
    };

    private static bool LooksLikeFarewell(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return false;

        var normalized = reply.ToLowerInvariant();
        return FarewellMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private bool TryLocateReplyKey()
    {
        var rawText = _rawResponse.ToString();
        var keyIndex = rawText.IndexOf(ReplyKeyToken, _nextUnprocessedIndex, StringComparison.Ordinal);
        if (keyIndex >= 0)
        {
            _nextUnprocessedIndex = keyIndex + ReplyKeyToken.Length;
            _state = ParserState.SeekingColon;
            return true;
        }

        _nextUnprocessedIndex = Math.Max(_nextUnprocessedIndex, rawText.Length - (ReplyKeyToken.Length - 1));
        return false;
    }

    private static bool ResolveEndCallFlag(string rawText)
    {
        try
        {
            using var parsedDocument = JsonDocument.Parse(StripMarkdownCodeFence(rawText));
            if (parsedDocument.RootElement.ValueKind == JsonValueKind.Object &&
                parsedDocument.RootElement.TryGetProperty("endCall", out var endCallElement) &&
                (endCallElement.ValueKind == JsonValueKind.True || endCallElement.ValueKind == JsonValueKind.False))
            {
                return endCallElement.GetBoolean();
            }
        }
        catch (JsonException) { }

        var endCallMatch = Regex.Match(rawText, "\"endCall\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
        return endCallMatch.Success && bool.Parse(endCallMatch.Groups[1].Value.ToLowerInvariant());
    }

    private static string? ResolveEndCallReason(string rawText)
    {
        try
        {
            using var parsedDocument = JsonDocument.Parse(StripMarkdownCodeFence(rawText));
            if (parsedDocument.RootElement.ValueKind == JsonValueKind.Object &&
                parsedDocument.RootElement.TryGetProperty("endCallReason", out var reasonElement) &&
                reasonElement.ValueKind == JsonValueKind.String)
            {
                var reason = reasonElement.GetString()?.Trim();
                return string.IsNullOrEmpty(reason) ? null : reason;
            }
        }
        catch (JsonException) { }

        var reasonMatch = Regex.Match(rawText, "\"endCallReason\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (!reasonMatch.Success)
            return null;

        var matchedReason = reasonMatch.Groups[1].Value.Trim();
        return string.IsNullOrEmpty(matchedReason) ? null : matchedReason;
    }

    private static string ExtractFallbackReply(string rawText)
    {
        var withoutCodeFence = StripMarkdownCodeFence(rawText);

        try
        {
            using var parsedDocument = JsonDocument.Parse(withoutCodeFence);
            if (parsedDocument.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var candidateKey in new[] { "reply", "content", "text", "message" })
                {
                    if (parsedDocument.RootElement.TryGetProperty(candidateKey, out var replyElement) &&
                        replyElement.ValueKind == JsonValueKind.String)
                    {
                        return replyElement.GetString()?.Trim() ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException) { }

        return withoutCodeFence
            .Replace("[DIALOG_END]", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string StripMarkdownCodeFence(string text)
    {
        var trimmedText = text.Trim();
        if (!trimmedText.StartsWith("```", StringComparison.Ordinal))
            return trimmedText;

        var firstLineBreakIndex = trimmedText.IndexOf('\n');
        if (firstLineBreakIndex < 0)
            return trimmedText;

        var withoutOpeningFence = trimmedText[(firstLineBreakIndex + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }
}
