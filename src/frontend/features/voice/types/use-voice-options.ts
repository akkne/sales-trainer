export interface VoiceCompanyContext {
    companyName: string;
    companyDescription: string;
    callGoal?: string;
}

export interface UseVoiceOptions {
    sessionId: string | null;
    modeVoiceEnabled: boolean;
    bundleId?: string;
    modeId?: string;
    companyContext?: VoiceCompanyContext;
    onSessionCreated?: (sessionId: string) => void;
    onTranscript?: (transcript: string) => void;
    onAiText?: (textChunk: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}
