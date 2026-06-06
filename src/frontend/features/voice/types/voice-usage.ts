export interface VoiceUsage {
    dailyUsedSeconds: number;
    dailyLimitSeconds: number;
    monthlyUsedSeconds: number;
    monthlyLimitSeconds: number;
    dailyExceeded: boolean;
    monthlyExceeded: boolean;
}
