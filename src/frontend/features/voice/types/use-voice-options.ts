export interface VoiceCompanyContext {
    companyName: string;
    companyDescription: string;
    callGoal?: string;
    personaName?: string;
    personaPosition?: string;
    personaPersonality?: string;
    personaDifficulty?: string;
}

export interface UseVoiceOptions {
    sessionId: string | null;
    modeVoiceEnabled: boolean;
    bundleId?: string;
    modeId?: string;
    companyContext?: VoiceCompanyContext;
    /** The call has a live session — either just created, or the one handed in via `sessionId`. */
    onSessionReady?: (sessionId: string) => void;
    onTranscript?: (transcript: string) => void;
    onAiText?: (textChunk: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}
