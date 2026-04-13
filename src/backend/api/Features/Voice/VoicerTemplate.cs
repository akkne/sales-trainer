namespace SalesTrainer.Api.Features.Voice;

public sealed class VoicerTemplate
{
    public string VoiceId { get; set; } = null!;
    public string PublicOwnerId { get; set; } = null!;
    public string? ModelId { get; set; }
    public VoicerVoiceSettings? VoiceSettings { get; set; }
}
