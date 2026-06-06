export interface VoiceConfig {
    enabled: boolean;
    vadSilenceMs: number;
    maxRecordingSeconds: number;
    dailyLimitMinutes: number;
    monthlyLimitMinutes: number;
}
