namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class GoogleTtsAudioConfiguration
{
    public string AudioEncoding { get; set; } = null!;
    public double SpeakingRate { get; set; }
    public double Pitch { get; set; }
}
