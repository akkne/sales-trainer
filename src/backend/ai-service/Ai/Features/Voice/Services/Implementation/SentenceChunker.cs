using System.Text;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Splits a streamed LLM reply into chunks suitable for incremental TTS synthesis.
/// The first chunk additionally splits on clause delimiters (comma, semicolon, colon, dash)
/// with a lower minimum length, so the first audio reaches the user as early as possible.
/// Subsequent chunks split on sentence enders only, keeping natural TTS prosody.
///
/// <para>
/// A clause delimiter counts only before the first chunk and only when whitespace follows it, so a
/// decimal written the Russian way ("1,5") is never cut in half.
/// </para>
///
/// <para>
/// Stateful and single-threaded: one instance belongs to one turn.
/// </para>
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
        for (var index = minimumLength; index < text.Length; index++)
        {
            if (IsSentenceDelimiter(text[index]))
            {
                splitIndex = index;
                break;
            }

            if (!_firstChunkEmitted && IsClauseDelimiter(text[index])
                && index + 1 < text.Length && char.IsWhiteSpace(text[index + 1]))
            {
                splitIndex = index;
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

    private static bool IsSentenceDelimiter(char character) => character is '.' or '!' or '?' or '\n';

    private static bool IsClauseDelimiter(char character) => character is ',' or ';' or ':' or '—' or '–';
}
