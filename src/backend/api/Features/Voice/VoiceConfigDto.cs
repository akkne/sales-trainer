namespace SalesTrainer.Api.Features.Voice;

public class VoiceConfigDto
{
    public bool Enabled { get; set; }
    public int VadSilenceMs { get; set; }
    public int MaxRecordingSeconds { get; set; }
    public DeepgramConfigDto Deepgram { get; set; } = new();
}

public class DeepgramConfigDto
{
    public bool Configured { get; set; }
    public string Model { get; set; } = "nova-3";
    public string Language { get; set; } = "ru";
    public bool SmartFormat { get; set; } = true;
    public bool Punctuate { get; set; } = true;
}
