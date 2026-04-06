import { useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { apiClient } from "@/lib/api/apiClient";
import { VadManager, VadState, floatTo16BitPCM } from "@/lib/voice/vadManager";
import { DeepgramClient, DeepgramState } from "@/lib/voice/deepgramClient";
import { AudioPlayer, AudioPlayerState } from "@/lib/voice/audioPlayer";

export interface VoiceConfig {
    enabled: boolean;
    vadSilenceMs: number;
    maxRecordingSeconds: number;
    deepgram: {
        configured: boolean;
        model: string;
        language: string;
        smartFormat: boolean;
        punctuate: boolean;
    };
}

export interface DeepgramKeyResponse {
    apiKey: string;
}

export function useVoiceConfig() {
    return useQuery({
        queryKey: ["voice", "config"],
        queryFn: () => apiClient.get<VoiceConfig>("/dialog/voice/config"),
        staleTime: 5 * 60 * 1000, // 5 minutes
    });
}

export function useDeepgramKey(enabled: boolean) {
    return useQuery({
        queryKey: ["voice", "deepgram-key"],
        queryFn: () => apiClient.get<DeepgramKeyResponse>("/dialog/voice/deepgram-key"),
        enabled,
        staleTime: 5 * 60 * 1000,
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
    onTranscript?: (transcript: string) => void;
    onAiResponse?: (content: string, isStopSignal: boolean) => void;
    onError?: (error: Error) => void;
}

export function useVoice(options: UseVoiceOptions) {
    const { sessionId, modeVoiceEnabled, onTranscript, onAiResponse, onError } = options;

    const [state, setState] = useState<VoicePipelineState>("idle");
    const [currentTranscript, setCurrentTranscript] = useState("");
    const [isVoiceAvailable, setIsVoiceAvailable] = useState(false);

    const vadRef = useRef<VadManager | null>(null);
    const deepgramRef = useRef<DeepgramClient | null>(null);
    const audioPlayerRef = useRef<AudioPlayer | null>(null);
    const transcriptBufferRef = useRef<string>("");

    const { data: voiceConfig } = useVoiceConfig();
    const { data: deepgramKey } = useDeepgramKey(
        !!voiceConfig?.enabled && modeVoiceEnabled
    );

    // Check if voice is available
    useEffect(() => {
        const available = !!(
            voiceConfig?.enabled &&
            modeVoiceEnabled &&
            deepgramKey?.apiKey
        );
        setIsVoiceAvailable(available);
    }, [voiceConfig, modeVoiceEnabled, deepgramKey]);

    // Initialize audio player
    useEffect(() => {
        audioPlayerRef.current = new AudioPlayer({
            onStateChange: (playerState: AudioPlayerState) => {
                if (playerState === "playing") {
                    setState("playing");
                } else if (playerState === "ended") {
                    setState("listening");
                    // Resume VAD after playback
                    vadRef.current?.resume();
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

    const startVoice = useCallback(async () => {
        if (!isVoiceAvailable || !deepgramKey?.apiKey || !sessionId) {
            return;
        }

        setState("initializing");

        try {
            // Initialize Deepgram
            deepgramRef.current = new DeepgramClient(
                {
                    apiKey: deepgramKey.apiKey,
                    model: voiceConfig?.deepgram.model,
                    language: voiceConfig?.deepgram.language,
                    smartFormat: voiceConfig?.deepgram.smartFormat,
                    punctuate: voiceConfig?.deepgram.punctuate,
                },
                {
                    onTranscript: (transcript: string, isFinal: boolean) => {
                        if (isFinal) {
                            transcriptBufferRef.current += (transcriptBufferRef.current ? " " : "") + transcript;
                            setCurrentTranscript(transcriptBufferRef.current);
                        } else {
                            setCurrentTranscript(transcriptBufferRef.current + (transcriptBufferRef.current ? " " : "") + transcript);
                        }
                    },
                    onError: (error: Error) => {
                        onError?.(error);
                        setState("error");
                    },
                    onStateChange: (deepgramState: DeepgramState) => {
                        if (deepgramState === "error") {
                            setState("error");
                        }
                    },
                }
            );

            await deepgramRef.current.connect();

            // Initialize VAD
            vadRef.current = new VadManager({
                onSpeechStart: () => {
                    setState("speaking");
                },
                onSpeechEnd: async (audio: Float32Array) => {
                    setState("processing");

                    // Send final audio to Deepgram
                    const pcmAudio = floatTo16BitPCM(audio);
                    deepgramRef.current?.sendAudio(pcmAudio);

                    // Wait a bit for final transcript
                    await new Promise((resolve) => setTimeout(resolve, 200));

                    const finalTranscript = transcriptBufferRef.current.trim();
                    if (finalTranscript) {
                        onTranscript?.(finalTranscript);

                        // Send to backend and get audio response
                        try {
                            vadRef.current?.pause();

                            const response = await fetch(
                                `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/dialog/sessions/${sessionId}/voice`,
                                {
                                    method: "POST",
                                    headers: {
                                        "Content-Type": "application/json",
                                        Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
                                    },
                                    body: JSON.stringify({ transcript: finalTranscript }),
                                }
                            );

                            if (!response.ok) {
                                throw new Error(`Voice request failed: ${response.status}`);
                            }

                            // Get AI response content from header or separate request
                            const voiceResponseRes = await apiClient.get<{
                                content: string;
                                isStopSignal: boolean;
                            }>(`/dialog/sessions/${sessionId}/voice/response`);

                            onAiResponse?.(voiceResponseRes.content, voiceResponseRes.isStopSignal);

                            // Play audio
                            const audioBlob = await response.blob();
                            await audioPlayerRef.current?.playBlob(audioBlob);
                        } catch (error) {
                            onError?.(error instanceof Error ? error : new Error("Voice processing failed"));
                            setState("error");
                            vadRef.current?.resume();
                        }
                    } else {
                        setState("listening");
                    }

                    // Clear transcript buffer
                    transcriptBufferRef.current = "";
                    setCurrentTranscript("");
                },
            });

            await vadRef.current.start();
            setState("listening");
        } catch (error) {
            onError?.(error instanceof Error ? error : new Error("Voice initialization failed"));
            setState("error");
        }
    }, [isVoiceAvailable, deepgramKey, sessionId, voiceConfig, onTranscript, onAiResponse, onError]);

    const stopVoice = useCallback(async () => {
        vadRef.current?.stop();
        deepgramRef.current?.disconnect();
        audioPlayerRef.current?.stop();

        vadRef.current = null;
        deepgramRef.current = null;

        setState("idle");
        setCurrentTranscript("");
        transcriptBufferRef.current = "";
    }, []);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            vadRef.current?.stop();
            deepgramRef.current?.disconnect();
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
