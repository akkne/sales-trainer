import { useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { apiClient } from "@/lib/api/apiClient";
import { WebSpeechClient, WebSpeechState, isWebSpeechSupported } from "@/lib/voice/webSpeechClient";
import { AudioPlayer, AudioPlayerState } from "@/lib/voice/audioPlayer";
import { readVoiceStream } from "@/lib/voice/streamReader";

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
        staleTime: 5 * 60 * 1000,
    });
}

export interface VoiceUsage {
    dailyUsedSeconds: number;
    dailyLimitSeconds: number;
    monthlyUsedSeconds: number;
    monthlyLimitSeconds: number;
    dailyExceeded: boolean;
    monthlyExceeded: boolean;
}

export function useVoiceUsage(enabled = true) {
    return useQuery({
        queryKey: ["voice", "usage"],
        queryFn: () => apiClient.get<VoiceUsage>("/dialog/voice/usage"),
        staleTime: 30 * 1000,
        enabled,
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
    onAiText?: (textChunk: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}

export function useVoice(options: UseVoiceOptions) {
    const { sessionId, modeVoiceEnabled, bundleId, modeId, onSessionCreated, onTranscript, onAiText, onAiResponse, onError } = options;

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
    const streamAbortRef = useRef<AbortController | null>(null);

    useEffect(() => {
        currentSessionIdRef.current = sessionId;
    }, [sessionId]);

    useEffect(() => {
        stateRef.current = state;
    }, [state]);

    const { data: voiceConfig } = useVoiceConfig();

    useEffect(() => {
        const available = !!(voiceConfig?.enabled && modeVoiceEnabled && isWebSpeechSupported());
        setIsVoiceAvailable(available);
    }, [voiceConfig, modeVoiceEnabled]);

    useEffect(() => {
        audioPlayerRef.current = new AudioPlayer({
            onStateChange: (playerState: AudioPlayerState) => {
                if (playerState === "playing") {
                    setState("playing");
                } else if (playerState === "ended") {
                    setState("listening");
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

    const processSpeech = useCallback(async (transcript: string) => {
        if (isProcessingRef.current) return;

        const activeSessionId = currentSessionIdRef.current;
        if (!transcript.trim() || !activeSessionId) {
            setState("listening");
            return;
        }

        isProcessingRef.current = true;
        setState("processing");
        onTranscript?.(transcript);

        const controller = new AbortController();
        streamAbortRef.current = controller;

        try {
            speechClientRef.current?.pause();

            const apiBase = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
            const response = await fetch(`${apiBase}/dialog/sessions/${activeSessionId}/voice/stream`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
                },
                body: JSON.stringify({ transcript }),
                signal: controller.signal,
            });

            if (response.status === 429) {
                const body = await response.json().catch(() => ({} as Record<string, unknown>));
                const period = (body.period as string) ?? "daily";
                const limit = Math.round(((body.limitSeconds as number) ?? 0) / 60);
                throw new Error(
                    period === "monthly"
                        ? `Месячный лимит звонков (${limit} мин) исчерпан`
                        : `Дневной лимит звонков (${limit} мин) исчерпан`,
                );
            }
            if (!response.ok) {
                throw new Error(`Voice request failed: ${response.status}`);
            }

            audioPlayerRef.current?.beginQueue();
            const aggregatedText: string[] = [];
            let stopSignal = false;
            let firstAudioPlayed = false;

            for await (const frame of readVoiceStream(response, controller.signal)) {
                if (controller.signal.aborted) break;
                if (frame.text) {
                    aggregatedText.push(frame.text);
                    onAiText?.(frame.text);
                }
                if (frame.isStopSignal) stopSignal = true;
                if (frame.audio.byteLength > 0) {
                    await audioPlayerRef.current?.enqueue(frame.audio);
                    if (!firstAudioPlayed) {
                        firstAudioPlayed = true;
                        speechClientRef.current?.resume();
                    }
                }
                if (frame.isFinal) break;
            }

            if (!controller.signal.aborted) {
                audioPlayerRef.current?.markQueueComplete();
                onAiResponse?.(aggregatedText.join(" "), stopSignal);
            }
        } catch (error) {
            if ((error as Error).name !== "AbortError" && !controller.signal.aborted) {
                onError?.(error instanceof Error ? error : new Error("Voice processing failed"));
                setState("error");
            }
            speechClientRef.current?.resume();
        } finally {
            isProcessingRef.current = false;
            if (streamAbortRef.current === controller) {
                streamAbortRef.current = null;
            }
        }

        transcriptBufferRef.current = "";
        setCurrentTranscript("");
    }, [onTranscript, onAiText, onAiResponse, onError]);

    const startVoice = useCallback(async () => {
        if (!isVoiceAvailable) return;

        setState("initializing");

        let activeSessionId = currentSessionIdRef.current;
        if (!activeSessionId && bundleId && modeId) {
            try {
                const session = await apiClient.post<{ id: string }>("/dialog/sessions", { bundleId, modeId });
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
                    if (silenceTimeoutRef.current) {
                        clearTimeout(silenceTimeoutRef.current);
                        silenceTimeoutRef.current = null;
                    }

                    if (isFinal) {
                        transcriptBufferRef.current += (transcriptBufferRef.current ? " " : "") + transcript;
                        setCurrentTranscript(transcriptBufferRef.current);

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
                    if (speechState === "listening") setState("listening");
                    else if (speechState === "error") setState("error");
                },
                onSpeechStart: () => {
                    if (stateRef.current === "playing") {
                        audioPlayerRef.current?.stop();
                        streamAbortRef.current?.abort();
                        streamAbortRef.current = null;
                    }
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

        streamAbortRef.current?.abort();
        streamAbortRef.current = null;

        speechClientRef.current?.stop();
        audioPlayerRef.current?.stop();

        speechClientRef.current = null;

        setState("idle");
        setCurrentTranscript("");
        transcriptBufferRef.current = "";
    }, []);

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
