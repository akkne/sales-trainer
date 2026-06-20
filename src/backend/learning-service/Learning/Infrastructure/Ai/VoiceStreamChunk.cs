namespace Sellevate.Learning.Infrastructure.Ai;

public sealed record VoiceStreamChunk(
    string Text,
    byte[] AudioMp3,
    bool IsStopSignal,
    bool IsFinal);
