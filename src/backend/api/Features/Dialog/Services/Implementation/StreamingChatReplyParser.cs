using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SalesTrainer.Api.Features.Dialog.Services.Implementation;

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
            return new ChatReplyParseResult(
                Reply: _decodedReply.ToString().Trim(),
                EndCall: ResolveEndCallFlag(rawText),
                UsedFallback: false);
        }

        var fallbackReply = ExtractFallbackReply(rawText);
        return new ChatReplyParseResult(
            Reply: fallbackReply,
            EndCall: ResolveEndCallFlag(rawText) || rawText.Contains("[DIALOG_END]", StringComparison.Ordinal),
            UsedFallback: true);
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
