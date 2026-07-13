import { useCallback, useEffect, useRef, useState } from "react";
import { EnvironmentConfiguration } from "@/config/environment";
import { WebSpeechClient, isWebSpeechSupported } from "@/features/voice/services/web-speech-client";
import type { WebSpeechState } from "@/features/voice/services/web-speech-client";
import { AudioPlayer } from "@/features/voice/services/audio-player";
import type { AudioPlayerState } from "@/features/voice/services/audio-player";
import { SpeechEndpointer } from "@/features/voice/services/speech-endpointer";
import { VoiceStreamReader } from "@/features/voice/services/voice-stream-reader";
import { useVoiceConfig } from "@/features/voice/hooks/use-voice-config";
import type { VoicePipelineState } from "@/features/voice/types/voice-pipeline-state";

export type { VoicePipelineState } from "@/features/voice/types/voice-pipeline-state";

interface UseExerciseVoiceOptions {
    exerciseId: string;
    onTranscript?: (transcript: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}

/**
 * Voice pipeline for ai_dialog exercises. Reuses the same STT/VAD/audio
 * services as live calls, but streams from the exercise voice endpoint
 * (`/exercises/{id}/voice/stream`) instead of a dialogue session.
 */
export function useExerciseVoice(options: UseExerciseVoiceOptions) {
    const { exerciseId, onTranscript, onAiResponse, onError } = options;

    const [state, setState] = useState<VoicePipelineState>("idle");
    const [currentTranscript, setCurrentTranscript] = useState("");
    const [isVoiceAvailable, setIsVoiceAvailable] = useState(false);

    const speechClientRef = useRef<WebSpeechClient | null>(null);
    const audioPlayerRef = useRef<AudioPlayer | null>(null);
    const endpointerRef = useRef<SpeechEndpointer | null>(null);
    const isProcessingRef = useRef(false);
    const stateRef = useRef<VoicePipelineState>("idle");
    const streamAbortRef = useRef<AbortController | null>(null);

    useEffect(() => {
        stateRef.current = state;
    }, [state]);

    const { data: voiceConfig } = useVoiceConfig();

    useEffect(() => {
        setIsVoiceAvailable(!!(voiceConfig?.enabled && isWebSpeechSupported()));
    }, [voiceConfig]);

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
        if (!transcript.trim()) {
            setState("listening");
            return;
        }

        isProcessingRef.current = true;
        setState("processing");
        onTranscript?.(transcript);

        endpointerRef.current?.reset();
        setCurrentTranscript("");

        const controller = new AbortController();
        streamAbortRef.current = controller;

        try {
            speechClientRef.current?.pause();

            const response = await fetch(
                `${EnvironmentConfiguration.apiBaseUrl}/exercises/${exerciseId}/voice/stream`,
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
                    },
                    body: JSON.stringify({ message: transcript }),
                    signal: controller.signal,
                },
            );

            if (response.status === 429) {
                const body = await response.json().catch(() => ({} as Record<string, unknown>));
                const period = (body.period as string) ?? "daily";
                const limit = Math.round(((body.limitSeconds as number) ?? 0) / 60);
                throw new Error(
                    period === "monthly"
                        ? `Достигнут месячный лимит голоса (${limit} мин)`
                        : `Достигнут дневной лимит голоса (${limit} мин)`,
                );
            }
            if (!response.ok) {
                throw new Error(`Ошибка голосового запроса: ${response.status}`);
            }

            audioPlayerRef.current?.beginQueue();
            const aggregatedText: string[] = [];
            let stopSignal = false;
            let firstAudioPlayed = false;

            const streamReader = new VoiceStreamReader(response);
            for await (const frame of streamReader.read(controller.signal)) {
                if (controller.signal.aborted) break;
                if (frame.text) {
                    aggregatedText.push(frame.text);
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
                if (stopSignal) {
                    speechClientRef.current?.pause();
                }
            }
        } catch (error) {
            if ((error as Error).name !== "AbortError" && !controller.signal.aborted) {
                onError?.(error instanceof Error ? error : new Error("Ошибка обработки голоса"));
                setState("error");
            }
            speechClientRef.current?.resume();
        } finally {
            isProcessingRef.current = false;
            if (streamAbortRef.current === controller) {
                streamAbortRef.current = null;
            }
        }
    }, [exerciseId, onTranscript, onAiResponse, onError]);

    const startVoice = useCallback(async () => {
        if (!isVoiceAvailable) return;

        setState("initializing");

        try {
            endpointerRef.current = new SpeechEndpointer({
                silenceMs: voiceConfig?.vadSilenceMs ?? 600,
                onUtterance: (utterance: string) => {
                    processSpeech(utterance);
                },
            });

            speechClientRef.current = new WebSpeechClient({
                language: "ru-RU",
                continuous: true,
                interimResults: true,
                onResult: (transcript: string, isFinal: boolean) => {
                    if (isProcessingRef.current) return;
                    endpointerRef.current?.handleResult(transcript, isFinal);
                    setCurrentTranscript(endpointerRef.current?.currentText ?? "");
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
            onError?.(error instanceof Error ? error : new Error("Ошибка инициализации голоса"));
            setState("error");
        }
    }, [isVoiceAvailable, voiceConfig, onError, processSpeech]);

    const stopVoice = useCallback(() => {
        endpointerRef.current?.reset();
        endpointerRef.current = null;

        streamAbortRef.current?.abort();
        streamAbortRef.current = null;

        speechClientRef.current?.stop();
        audioPlayerRef.current?.stop();

        speechClientRef.current = null;

        setState("idle");
        setCurrentTranscript("");
    }, []);

    useEffect(() => {
        return () => {
            endpointerRef.current?.reset();
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
