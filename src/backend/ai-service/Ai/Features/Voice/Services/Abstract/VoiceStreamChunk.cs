namespace Sellevate.Ai.Features.Voice.Services.Abstract;

public sealed record VoiceStreamChunk(string Text, byte[] AudioMp3, bool IsStopSignal, bool IsFinal);
