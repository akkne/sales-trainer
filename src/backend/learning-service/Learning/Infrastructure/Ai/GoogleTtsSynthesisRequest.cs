namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class GoogleTtsSynthesisRequest
{
    public GoogleTtsSynthesisInput Input { get; set; } = null!;
    public GoogleTtsSynthesisVoice Voice { get; set; } = null!;
    public GoogleTtsAudioConfiguration AudioConfig { get; set; } = null!;
}
