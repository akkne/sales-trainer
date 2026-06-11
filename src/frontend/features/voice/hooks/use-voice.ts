import { useCallback, useEffect, useRef, useState } from "react";
import { apiClient } from "@/shared/api/api-client";
import { EnvironmentConfiguration } from "@/config/environment";
import { WebSpeechClient, isWebSpeechSupported } from "@/features/voice/services/web-speech-client";
import type { WebSpeechState } from "@/features/voice/services/web-speech-client";
import { AudioPlayer } from "@/features/voice/services/audio-player";
import type { AudioPlayerState } from "@/features/voice/services/audio-player";
import { SpeechEndpointer } from "@/features/voice/services/speech-endpointer";
import { VoiceStreamReader } from "@/features/voice/services/voice-stream-reader";
import { VoiceApiRoutes } from "@/features/voice/constants/voice-api-routes";
import { useVoiceConfig } from "@/features/voice/hooks/use-voice-config";
import type { VoicePipelineState } from "@/features/voice/types/voice-pipeline-state";
import type { UseVoiceOptions } from "@/features/voice/types/use-voice-options";

export type { VoicePipelineState } from "@/features/voice/types/voice-pipeline-state";
export type { UseVoiceOptions } from "@/features/voice/types/use-voice-options";

export function useVoice(options: UseVoiceOptions) {
    const { sessionId, modeVoiceEnabled, bundleId, modeId, onSessionCreated, onTranscript, onAiText, onAiResponse, onError } = options;

    const [state, setState] = useState<VoicePipelineState>("idle");
    const [currentTranscript, setCurrentTranscript] = useState("");
    const [isVoiceAvailable, setIsVoiceAvailable] = useState(false);

    const speechClientRef = useRef<WebSpeechClient | null>(null);
    const audioPlayerRef = useRef<AudioPlayer | null>(null);
    const endpointerRef = useRef<SpeechEndpointer | null>(null);
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

        // The phrase is committed to the subtitle history now — clear the interim
        // line immediately so it does not reappear as a duplicate once the
        // pipeline returns to listening/speaking.
        endpointerRef.current?.reset();
        setCurrentTranscript("");

        const controller = new AbortController();
        streamAbortRef.current = controller;

        try {
            speechClientRef.current?.pause();

            const response = await fetch(`${EnvironmentConfiguration.apiBaseUrl}${VoiceApiRoutes.stream(activeSessionId)}`, {
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

            const streamReader = new VoiceStreamReader(response);
            for await (const frame of streamReader.read(controller.signal)) {
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
                    // The utterance was already committed from an interim result;
                    // drop trailing recognition so it does not duplicate the phrase.
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
            onError?.(error instanceof Error ? error : new Error("Voice initialization failed"));
            setState("error");
        }
    }, [isVoiceAvailable, bundleId, modeId, voiceConfig, onSessionCreated, onError, processSpeech]);

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
