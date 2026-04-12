"use client";

import { useParams, useRouter } from "next/navigation";
import { useState, useEffect, useCallback, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
    useDialogBundles,
    useDialogModes,
    completeDialogSession,
    DialogFeedback,
} from "@/lib/hooks/useDialog";
import { useVoice, VoicePipelineState } from "@/lib/hooks/useVoice";
import { apiClient } from "@/lib/api/apiClient";
import { FeedbackModal } from "@/components/dialog/FeedbackModal";
import { Icon } from "@/components/ui/Icon";

function formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
}

export default function VoiceOnlyPage() {
    const params = useParams();
    const router = useRouter();
    const queryClient = useQueryClient();
    const bundleId = params.bundleId as string;
    const modeId = params.modeId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes } = useDialogModes(bundleId);

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);
    const currentMode = modes?.find((mode) => mode.id === modeId);

    const [sessionId, setSessionId] = useState<string | null>(null);
    const [isEnded, setIsEnded] = useState(false);
    const [feedback, setFeedback] = useState<DialogFeedback | null>(null);
    const [isCompleting, setIsCompleting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [sessionTimer, setSessionTimer] = useState(0);

    const timerRef = useRef<NodeJS.Timeout | null>(null);

    // Session timer
    useEffect(() => {
        if (sessionId && !isEnded && !feedback) {
            timerRef.current = setInterval(() => {
                setSessionTimer((prev) => prev + 1);
            }, 1000);
        } else {
            if (timerRef.current) {
                clearInterval(timerRef.current);
                timerRef.current = null;
            }
        }
        return () => {
            if (timerRef.current) {
                clearInterval(timerRef.current);
            }
        };
    }, [sessionId, isEnded, feedback]);

    // Reset timer on new session
    useEffect(() => {
        if (sessionId) {
            setSessionTimer(0);
        }
    }, [sessionId]);

    const handleVoiceError = useCallback((err: Error) => {
        setError(err.message);
        setTimeout(() => setError(null), 5000);
    }, []);

    const handleVoiceSessionCreated = useCallback((newSessionId: string) => {
        setSessionId(newSessionId);
    }, []);

    const handleAiResponse = useCallback((content: string, isStopSignal: boolean) => {
        if (isStopSignal && sessionId) {
            setIsEnded(true);
            // Auto-complete session
            completeSession(sessionId);
        }
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [sessionId]);

    const completeSession = async (sid: string) => {
        if (isCompleting) return;
        setIsCompleting(true);
        try {
            const sessionFeedback = await completeDialogSession(sid);
            setFeedback(sessionFeedback);
            queryClient.invalidateQueries({ queryKey: ["profile"] });
        } catch (err) {
            setError(err instanceof Error ? err.message : "Error completing session");
        } finally {
            setIsCompleting(false);
        }
    };

    const {
        state: voiceState,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    } = useVoice({
        sessionId,
        modeVoiceEnabled: currentMode?.voiceEnabled ?? false,
        bundleId,
        modeId,
        onSessionCreated: handleVoiceSessionCreated,
        onAiResponse: handleAiResponse,
        onError: handleVoiceError,
    });

    const handleClose = () => {
        router.push("/dialog");
    };

    const handleEndSession = () => {
        if (sessionId && !isEnded) {
            setIsEnded(true);
            stopVoice();
            completeSession(sessionId);
        }
    };

    const handleCloseFeedback = () => {
        setFeedback(null);
        setSessionId(null);
        setIsEnded(false);
        setSessionTimer(0);
    };

    const handleStartNewSession = () => {
        setSessionId(null);
        setIsEnded(false);
        setFeedback(null);
        setSessionTimer(0);
    };

    const isActive = voiceState !== "idle" && voiceState !== "error";
    const isSpeaking = voiceState === "speaking";
    const isProcessing = voiceState === "processing";
    const isPlaying = voiceState === "playing";
    const isListening = voiceState === "listening";
    const isInitializing = voiceState === "initializing";

    const handleMicClick = () => {
        if (isActive) {
            stopVoice();
        } else {
            startVoice();
        }
    };

    // Status text and icon
    const getStatusInfo = (): { text: string; icon: string; color: string } => {
        switch (voiceState) {
            case "idle":
                return { text: "Tap to start", icon: "mic_off", color: "text-on-surface-variant" };
            case "initializing":
                return { text: "Starting...", icon: "mic", color: "text-primary" };
            case "listening":
                return { text: "Listening...", icon: "mic", color: "text-primary" };
            case "speaking":
                return { text: "Listening...", icon: "mic", color: "text-primary" };
            case "processing":
                return { text: "Processing...", icon: "sync", color: "text-secondary" };
            case "playing":
                return { text: "AI speaking...", icon: "volume_up", color: "text-tertiary" };
            case "error":
                return { text: "Error", icon: "error", color: "text-error" };
            default:
                return { text: "", icon: "mic", color: "text-on-surface-variant" };
        }
    };

    const statusInfo = getStatusInfo();

    // Voice not available
    if (!isVoiceAvailable && currentMode) {
        return (
            <div className="flex flex-col h-screen bg-surface">
                <header className="flex items-center justify-between px-4 py-3 border-b border-outline-variant bg-surface-container-lowest flex-shrink-0">
                    <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center">
                            <Icon name="psychology" size="md" className="text-secondary" />
                        </div>
                        <div>
                            <h1 className="font-semibold text-on-surface text-sm">
                                {currentMode?.title || "Voice Practice"}
                            </h1>
                            <p className="text-xs text-on-surface-variant">
                                {currentBundle?.title}
                            </p>
                        </div>
                    </div>
                    <button
                        onClick={handleClose}
                        className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                    >
                        <Icon name="close" size="md" className="text-on-surface-variant" />
                    </button>
                </header>

                <div className="flex-1 flex flex-col items-center justify-center p-8">
                    <div className="w-24 h-24 rounded-full bg-error-container flex items-center justify-center mb-6">
                        <Icon name="mic_off" size="xl" className="text-error" />
                    </div>
                    <p className="text-lg font-semibold text-on-surface mb-2">Voice not available</p>
                    <p className="text-sm text-on-surface-variant text-center max-w-xs">
                        Voice mode is not enabled for this exercise or your browser doesn't support it.
                    </p>
                    <button
                        onClick={handleClose}
                        className="mt-8 px-6 py-3 bg-primary text-on-primary font-semibold rounded-full"
                    >
                        Go Back
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="flex flex-col h-screen bg-surface">
            {/* Header */}
            <header className="flex items-center justify-between px-4 py-3 border-b border-outline-variant bg-surface-container-lowest flex-shrink-0">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center">
                        <Icon name="psychology" size="md" className="text-secondary" />
                    </div>
                    <div>
                        <h1 className="font-semibold text-on-surface text-sm">
                            {currentMode?.title || "Voice Practice"}
                        </h1>
                        <p className="text-xs text-on-surface-variant">
                            {currentBundle?.title}
                        </p>
                    </div>
                </div>

                <div className="flex items-center gap-2">
                    {/* Timer */}
                    {sessionId && (
                        <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-surface-container text-on-surface-variant">
                            <Icon name="timer" size="sm" />
                            <span className="text-sm font-mono font-medium tabular-nums">
                                {formatTime(sessionTimer)}
                            </span>
                        </div>
                    )}

                    {/* End session button */}
                    {sessionId && !isEnded && !feedback && (
                        <button
                            onClick={handleEndSession}
                            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-error-container text-error text-sm font-medium hover:opacity-90 tonal-transition"
                        >
                            <Icon name="stop_circle" size="sm" />
                            End
                        </button>
                    )}

                    {/* Close button */}
                    <button
                        onClick={handleClose}
                        className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                    >
                        <Icon name="close" size="md" className="text-on-surface-variant" />
                    </button>
                </div>
            </header>

            {/* Main content - big microphone button */}
            <div className="flex-1 flex flex-col items-center justify-center p-8">
                {/* Error message */}
                {error && (
                    <div className="absolute top-20 left-1/2 -translate-x-1/2 bg-error-container text-error text-sm px-4 py-2 rounded-full flex items-center gap-2">
                        <Icon name="error" size="sm" />
                        {error}
                    </div>
                )}

                {/* Completing indicator */}
                {isCompleting && (
                    <div className="absolute top-20 left-1/2 -translate-x-1/2 bg-surface-container text-on-surface-variant text-sm px-4 py-2 rounded-full flex items-center gap-2">
                        <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                        Generating feedback...
                    </div>
                )}

                {/* Status text above button */}
                <p className={`text-lg font-medium mb-8 ${statusInfo.color}`}>
                    {statusInfo.text}
                </p>

                {/* Big microphone button */}
                <button
                    onClick={handleMicClick}
                    disabled={isProcessing || isPlaying || isEnded || !!feedback}
                    className={`
                        relative w-40 h-40 rounded-full flex items-center justify-center
                        transition-all duration-300 shadow-xl
                        ${isActive
                            ? "bg-primary shadow-[0_8px_0_var(--color-primary-dim)]"
                            : "bg-surface-container-high hover:bg-surface-container-highest shadow-[0_8px_0_var(--color-outline-variant)]"
                        }
                        ${(isProcessing || isPlaying || isEnded || !!feedback) ? "opacity-50 cursor-not-allowed" : "cursor-pointer"}
                        active:translate-y-2 active:shadow-none
                    `}
                >
                    {/* Outer ring animation when speaking */}
                    {isSpeaking && (
                        <>
                            <div className="absolute inset-0 rounded-full animate-ping bg-primary opacity-20" />
                            <div className="absolute inset-[-8px] rounded-full border-4 border-primary animate-pulse opacity-50" />
                        </>
                    )}

                    {/* Listening pulse */}
                    {isListening && (
                        <div className="absolute inset-[-4px] rounded-full border-2 border-primary animate-pulse opacity-40" />
                    )}

                    {/* Playing waves */}
                    {isPlaying && (
                        <>
                            <div className="absolute inset-[-8px] rounded-full border-2 border-tertiary animate-pulse opacity-40" />
                            <div className="absolute inset-[-16px] rounded-full border border-tertiary animate-pulse opacity-20" style={{ animationDelay: "0.2s" }} />
                        </>
                    )}

                    {/* Icon */}
                    {isProcessing || isInitializing ? (
                        <div className={`w-12 h-12 border-4 border-current border-t-transparent rounded-full animate-spin ${isActive ? "text-on-primary" : "text-on-surface-variant"}`} />
                    ) : isPlaying ? (
                        <Icon
                            name="volume_up"
                            className={`text-6xl ${isActive ? "text-on-primary" : "text-on-surface-variant"}`}
                        />
                    ) : (
                        <Icon
                            name={isActive ? "mic" : "mic_off"}
                            variant={isActive ? "filled" : "outlined"}
                            className={`text-6xl ${isActive ? "text-on-primary" : "text-on-surface-variant"}`}
                        />
                    )}
                </button>

                {/* Instructions */}
                <p className="text-sm text-on-surface-variant mt-8 text-center max-w-xs">
                    {!sessionId && !isActive && "Tap the microphone to start your conversation"}
                    {isListening && "Speak naturally. AI will respond when you pause."}
                    {isSpeaking && "Go ahead, I'm listening..."}
                    {isPlaying && "Listen to the AI response"}
                    {isEnded && !feedback && "Session ended. Generating feedback..."}
                </p>

                {/* Start new session button after completion */}
                {feedback && (
                    <button
                        onClick={handleStartNewSession}
                        className="mt-8 px-6 py-3 bg-primary text-on-primary font-bold rounded-full shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition"
                    >
                        Start New Session
                    </button>
                )}
            </div>

            {/* Feedback modal */}
            {feedback && (
                <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />
            )}
        </div>
    );
}
