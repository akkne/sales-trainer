export interface UseVoiceOptions {
    sessionId: string | null;
    modeVoiceEnabled: boolean;
    bundleId?: string;
    modeId?: string;
    onSessionCreated?: (sessionId: string) => void;
    onTranscript?: (transcript: string) => void;
    onAiText?: (textChunk: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}
