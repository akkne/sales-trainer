using System.Text;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Splits a streamed LLM reply into chunks suitable for incremental TTS synthesis.
/// The first chunk additionally splits on clause delimiters (comma, semicolon, colon, dash)
/// with a lower minimum length, so the first audio reaches the user as early as possible.
/// Subsequent chunks split on sentence enders only, keeping natural TTS prosody.
/// </summary>
internal sealed class SentenceChunker
{
    private const int FirstChunkMinimumLength = 12;
    private const int SubsequentChunkMinimumLength = 20;

    private readonly StringBuilder _buffer = new();
    private bool _firstChunkEmitted;

    public void Append(string text) => _buffer.Append(text);

    /// <summary>Replaces the buffered text entirely (used for the plain-text fallback reply).</summary>
    public void Replace(string text)
    {
        _buffer.Clear();
        _buffer.Append(text);
    }

    /// <summary>Returns the buffered tail and clears the buffer.</summary>
    public string DrainRemaining()
    {
        var remaining = _buffer.ToString();
        _buffer.Clear();
        return remaining;
    }

    public bool TryExtractSentence(out string sentence)
    {
        var minimumLength = _firstChunkEmitted ? SubsequentChunkMinimumLength : FirstChunkMinimumLength;
        var text = _buffer.ToString();
        if (text.Length < minimumLength)
        {
            sentence = string.Empty;
            return false;
        }

        var splitIndex = -1;
        for (var i = minimumLength; i < text.Length; i++)
        {
            if (IsSentenceDelimiter(text[i]))
            {
                splitIndex = i;
                break;
            }

            // Clause delimiters count only before the first chunk and only when followed
            // by whitespace, so numbers like "1,5" are never cut in half.
            if (!_firstChunkEmitted && IsClauseDelimiter(text[i]) && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                splitIndex = i;
                break;
            }
        }

        if (splitIndex < 0)
        {
            sentence = string.Empty;
            return false;
        }

        sentence = text[..(splitIndex + 1)];
        _buffer.Remove(0, splitIndex + 1);
        _firstChunkEmitted = true;
        return true;
    }

    private static bool IsSentenceDelimiter(char ch) => ch is '.' or '!' or '?' or '\n';

    private static bool IsClauseDelimiter(char ch) => ch is ',' or ';' or ':' or '—' or '–';
}
