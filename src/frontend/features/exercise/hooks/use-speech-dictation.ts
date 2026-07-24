import { useCallback, useEffect, useRef, useState } from "react";
import { WebSpeechClient, isWebSpeechSupported } from "@/features/voice/services/web-speech-client";
import { useVoiceConfig } from "@/features/voice/hooks/use-voice-config";

export type DictationState = "idle" | "listening" | "error";

interface UseSpeechDictationOptions {
    /** Called with each finalized speech fragment so it can be appended to the field. */
    onFinalTranscript: (fragment: string) => void;
    onError?: (error: Error) => void;
    language?: string;
}

/**
 * Plain speech-to-text dictation on top of {@link WebSpeechClient}. Unlike
 * `useExerciseVoice`, it does NOT stream to the AI — it only transcribes the
 * user's speech and feeds finalized fragments back to the caller (e.g. to fill
 * a free-text answer field). Interim results are surfaced via `interimText`.
 */
export function useSpeechDictation({ onFinalTranscript, onError, language = "ru-RU" }: UseSpeechDictationOptions) {
    const [state, setState] = useState<DictationState>("idle");
    const [interimText, setInterimText] = useState("");
    const [isAvailable, setIsAvailable] = useState(false);

    const clientRef = useRef<WebSpeechClient | null>(null);
    const onFinalRef = useRef(onFinalTranscript);
    const onErrorRef = useRef(onError);

    useEffect(() => {
        onFinalRef.current = onFinalTranscript;
        onErrorRef.current = onError;
    }, [onFinalTranscript, onError]);

    const { data: voiceConfig } = useVoiceConfig();

    useEffect(() => {
        setIsAvailable(!!(voiceConfig?.enabled && isWebSpeechSupported()));
    }, [voiceConfig]);

    const stop = useCallback(() => {
        clientRef.current?.stop();
        clientRef.current = null;
        setInterimText("");
        setState("idle");
    }, []);

    const start = useCallback(async () => {
        if (!isAvailable || clientRef.current) return;

        const client = new WebSpeechClient({
            language,
            continuous: true,
            interimResults: true,
            onResult: (transcript, isFinal) => {
                if (isFinal) {
                    if (transcript) onFinalRef.current(transcript);
                    setInterimText("");
                } else {
                    setInterimText(transcript);
                }
            },
            onError: (error) => {
                onErrorRef.current?.(error);
                setState("error");
            },
            onStateChange: (speechState) => {
                if (speechState === "listening") setState("listening");
                else if (speechState === "error") setState("error");
            },
        });

        clientRef.current = client;
        await client.start();
    }, [isAvailable, language]);

    const toggle = useCallback(() => {
        if (clientRef.current) stop();
        else void start();
    }, [start, stop]);

    useEffect(() => {
        return () => {
            clientRef.current?.stop();
            clientRef.current = null;
        };
    }, []);

    return { state, interimText, isAvailable, isListening: state === "listening", start, stop, toggle };
}
