namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoicerVoiceSettings
{
    public double Stability { get; set; }
    public double SimilarityBoost { get; set; }
    public bool UseSpeakerBoost { get; set; }
    public double Speed { get; set; }
}
