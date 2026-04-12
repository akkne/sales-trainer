import { useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { apiClient } from "@/lib/api/apiClient";
import { WebSpeechClient, WebSpeechState, isWebSpeechSupported } from "@/lib/voice/webSpeechClient";
import { AudioPlayer, AudioPlayerState } from "@/lib/voice/audioPlayer";

export interface VoiceConfig {
    enabled: boolean;
    vadSilenceMs: number;
    maxRecordingSeconds: number;
    dailyLimitMinutes: number;
    monthlyLimitMinutes: number;
}

export function useVoiceConfig() {
    return useQuery({
        queryKey: ["voice", "config"],
        queryFn: () => apiClient.get<VoiceConfig>("/dialog/voice/config"),
        staleTime: 5 * 60 * 1000, // 5 minutes
    });
}

export type VoicePipelineState =
    | "idle"
    | "initializing"
    | "listening"
    | "speaking"
    | "processing"
    | "playing"
    | "error";

export interface UseVoiceOptions {
    sessionId: string | null;
    modeVoiceEnabled: boolean;
    bundleId?: string;
    modeId?: string;
    onSessionCreated?: (sessionId: string) => void;
    onTranscript?: (transcript: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}

export function useVoice(options: UseVoiceOptions) {
    const { sessionId, modeVoiceEnabled, bundleId, modeId, onSessionCreated, onTranscript, onAiResponse, onError } = options;

    const [state, setState] = useState<VoicePipelineState>("idle");
    const [currentTranscript, setCurrentTranscript] = useState("");
    const [isVoiceAvailable, setIsVoiceAvailable] = useState(false);

    const speechClientRef = useRef<WebSpeechClient | null>(null);
    const audioPlayerRef = useRef<AudioPlayer | null>(null);
    const transcriptBufferRef = useRef<string>("");
    const silenceTimeoutRef = useRef<NodeJS.Timeout | null>(null);
    const currentSessionIdRef = useRef<string | null>(sessionId);
    const isProcessingRef = useRef(false);
    const stateRef = useRef<VoicePipelineState>("idle");

    // Keep sessionId ref in sync
    useEffect(() => {
        currentSessionIdRef.current = sessionId;
    }, [sessionId]);

    // Keep state ref in sync
    useEffect(() => {
        stateRef.current = state;
    }, [state]);

    const { data: voiceConfig } = useVoiceConfig();

    // Check if voice is available
    useEffect(() => {
        const available = !!(
            voiceConfig?.enabled &&
            modeVoiceEnabled &&
            isWebSpeechSupported()
        );
        setIsVoiceAvailable(available);
    }, [voiceConfig, modeVoiceEnabled]);

    // Initialize audio player
    useEffect(() => {
        audioPlayerRef.current = new AudioPlayer({
            onStateChange: (playerState: AudioPlayerState) => {
                if (playerState === "playing") {
                    setState("playing");
                } else if (playerState === "ended") {
                    setState("listening");
                    // Resume speech recognition after playback
                    speechClientRef.current?.resume();
                } else if (playerState === "error") {
                    setState("error");
                }
            },
            onError: (error: Error) => {
                onError?.(error);
            },
        });

        return () => {
            audioPlayerRef.current?.destroy();
        };
    }, [onError]);

    // Process speech after silence
    const processSpeech = useCallback(async (transcript: string) => {
        // Prevent concurrent processing
        if (isProcessingRef.current) {
            return;
        }

        const activeSessionId = currentSessionIdRef.current;
        if (!transcript.trim() || !activeSessionId) {
            setState("listening");
            return;
        }

        isProcessingRef.current = true;
        setState("processing");
        onTranscript?.(transcript);

        try {
            // Pause speech recognition during processing
            speechClientRef.current?.pause();

            const response = await fetch(
                `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/dialog/sessions/${activeSessionId}/voice`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
                    },
                    body: JSON.stringify({ transcript }),
                }
            );

            if (!response.ok) {
                throw new Error(`Voice request failed: ${response.status}`);
            }

            // Get AI response content from separate request
            const voiceResponseRes = await apiClient.get<{
                content: string;
                isStopSignal: boolean;
            }>(`/dialog/sessions/${activeSessionId}/voice/response`);

            onAiResponse?.(voiceResponseRes.content, voiceResponseRes.isStopSignal);

            // Play audio
            const audioBlob = await response.blob();
            await audioPlayerRef.current?.playBlob(audioBlob);
        } catch (error) {
            onError?.(error instanceof Error ? error : new Error("Voice processing failed"));
            setState("error");
            speechClientRef.current?.resume();
        } finally {
            isProcessingRef.current = false;
        }

        // Clear transcript buffer
        transcriptBufferRef.current = "";
        setCurrentTranscript("");
    }, [onTranscript, onAiResponse, onError]);

    const startVoice = useCallback(async () => {
        if (!isVoiceAvailable) {
            return;
        }

        setState("initializing");

        // Create session if needed
        let activeSessionId = currentSessionIdRef.current;
        if (!activeSessionId && bundleId && modeId) {
            try {
                const session = await apiClient.post<{ id: string }>("/dialog/sessions", {
                    bundleId,
                    modeId,
                });
                activeSessionId = session.id;
                currentSessionIdRef.current = activeSessionId;
                onSessionCreated?.(activeSessionId);
            } catch (error) {
                onError?.(error instanceof Error ? error : new Error("Failed to create session"));
                setState("error");
                return;
            }
        }

        if (!activeSessionId) {
            onError?.(new Error("No session available"));
            setState("error");
            return;
        }

        try {
            speechClientRef.current = new WebSpeechClient({
                language: "ru-RU",
                continuous: true,
                interimResults: true,
                onResult: (transcript: string, isFinal: boolean) => {
                    // Clear silence timeout on new speech
                    if (silenceTimeoutRef.current) {
                        clearTimeout(silenceTimeoutRef.current);
                        silenceTimeoutRef.current = null;
                    }

                    if (isFinal) {
                        transcriptBufferRef.current += (transcriptBufferRef.current ? " " : "") + transcript;
                        setCurrentTranscript(transcriptBufferRef.current);

                        // Set silence timeout to process after pause
                        const vadSilenceMs = voiceConfig?.vadSilenceMs ?? 600;
                        silenceTimeoutRef.current = setTimeout(() => {
                            const finalTranscript = transcriptBufferRef.current.trim();
                            if (finalTranscript) {
                                processSpeech(finalTranscript);
                            }
                        }, vadSilenceMs);
                    } else {
                        setCurrentTranscript(transcriptBufferRef.current + (transcriptBufferRef.current ? " " : "") + transcript);
                    }
                },
                onError: (error: Error) => {
                    onError?.(error);
                    setState("error");
                },
                onStateChange: (speechState: WebSpeechState) => {
                    if (speechState === "listening") {
                        setState("listening");
                    } else if (speechState === "error") {
                        setState("error");
                    }
                },
                onSpeechStart: () => {
                    setState("speaking");
                },
                onSpeechEnd: () => {
                    if (stateRef.current !== "processing" && stateRef.current !== "playing") {
                        setState("listening");
                    }
                },
            });

            await speechClientRef.current.start();
        } catch (error) {
            onError?.(error instanceof Error ? error : new Error("Voice initialization failed"));
            setState("error");
        }
    }, [isVoiceAvailable, bundleId, modeId, voiceConfig, onSessionCreated, onError, processSpeech]);

    const stopVoice = useCallback(() => {
        if (silenceTimeoutRef.current) {
            clearTimeout(silenceTimeoutRef.current);
            silenceTimeoutRef.current = null;
        }

        speechClientRef.current?.stop();
        audioPlayerRef.current?.stop();

        speechClientRef.current = null;

        setState("idle");
        setCurrentTranscript("");
        transcriptBufferRef.current = "";
    }, []);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            if (silenceTimeoutRef.current) {
                clearTimeout(silenceTimeoutRef.current);
            }
            speechClientRef.current?.stop();
            audioPlayerRef.current?.destroy();
        };
    }, []);

    return {
        state,
        currentTranscript,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    };
}
