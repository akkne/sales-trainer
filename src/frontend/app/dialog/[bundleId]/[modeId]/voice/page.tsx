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
import { useVoice, useVoiceUsage, VoicePipelineState } from "@/lib/hooks/useVoice";
import { FeedbackModal } from "@/components/dialog/FeedbackModal";
import { GeoAvatar } from "@/components/ui/GeoAvatar";
import { Icon } from "@/components/ui/Icon";
import {
    startRingingTone,
    stopRingingTone,
    playHangupBeep,
    vibrateOnConnect,
} from "@/lib/voice/callSounds";

function formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
}

type CallStatus = "idle" | "dialing" | "connected" | "ended";

interface SubtitleEntry {
    role: "user" | "assistant";
    text: string;
    /** True when the user barged in and cut this AI reply short. */
    interrupted?: boolean;
}

interface StateInfo {
    label: string;
    hint: string;
    ringColor: string;
    pulse: boolean;
}

function describePipeline(state: VoicePipelineState, callStatus: CallStatus): StateInfo {
    if (callStatus === "idle") {
        return {
            label: "Готов к звонку",
            hint: "Нажмите «Позвонить», чтобы соединиться с собеседником",
            ringColor: "var(--line)",
            pulse: false,
        };
    }
    if (callStatus === "dialing" || state === "initializing") {
        return {
            label: "Соединение...",
            hint: "Гудки. Собеседник вот-вот возьмёт трубку",
            ringColor: "var(--indigo)",
            pulse: true,
        };
    }
    if (callStatus === "ended") {
        return {
            label: "Звонок завершён",
            hint: "Готовим разбор разговора",
            ringColor: "var(--ink-3)",
            pulse: false,
        };
    }
    switch (state) {
        case "listening":
            return {
                label: "На связи · слушаю вас",
                hint: "Говорите свободно — я отвечу, как только сделаете паузу",
                ringColor: "var(--olive)",
                pulse: false,
            };
        case "speaking":
            return {
                label: "Слышу вас",
                hint: "Продолжайте — фиксирую реплику",
                ringColor: "var(--olive)",
                pulse: true,
            };
        case "processing":
            return {
                label: "Думаю над ответом...",
                hint: "Готовлю реплику собеседника",
                ringColor: "var(--clay)",
                pulse: true,
            };
        case "playing":
            return {
                label: "Собеседник говорит",
                hint: "Прерывайте, когда захотите ответить",
                ringColor: "var(--rust)",
                pulse: true,
            };
        case "error":
            return {
                label: "Помехи на линии",
                hint: "Попробуйте позвонить ещё раз",
                ringColor: "var(--bad)",
                pulse: false,
            };
        default:
            return {
                label: "На линии",
                hint: "",
                ringColor: "var(--indigo)",
                pulse: false,
            };
    }
}

export default function VoiceCallPage() {
    const params = useParams();
    const router = useRouter();
    const queryClient = useQueryClient();
    const bundleId = params.bundleId as string;
    const modeId = params.modeId as string;

    const { data: bundles } = useDialogBundles();
    const { data: modes } = useDialogModes(bundleId);
    const { data: usage, refetch: refetchUsage } = useVoiceUsage();

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);
    const currentMode = modes?.find((mode) => mode.id === modeId);

    const [sessionId, setSessionId] = useState<string | null>(null);
    const [callStatus, setCallStatus] = useState<CallStatus>("idle");
    const [feedback, setFeedback] = useState<DialogFeedback | null>(null);
    const [isCompleting, setIsCompleting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [sessionTimer, setSessionTimer] = useState(0);
    const [subtitles, setSubtitles] = useState<SubtitleEntry[]>([]);

    const timerRef = useRef<NodeJS.Timeout | null>(null);
    const previousCallStatusRef = useRef<CallStatus>("idle");
    const previousVoiceStateRef = useRef<VoicePipelineState>("idle");
    // True while the AI's current reply is still streaming — new chunks are
    // appended to the last assistant entry instead of opening a new one.
    const assistantReplyOpenRef = useRef(false);
    const subtitleScrollRef = useRef<HTMLDivElement | null>(null);

    useEffect(() => {
        if (callStatus === "connected") {
            timerRef.current = setInterval(() => {
                setSessionTimer((prev) => prev + 1);
            }, 1000);
        } else if (timerRef.current) {
            clearInterval(timerRef.current);
            timerRef.current = null;
        }
        return () => {
            if (timerRef.current) {
                clearInterval(timerRef.current);
            }
        };
    }, [callStatus]);

    // Call sound effects + vibration tied to call status transitions.
    useEffect(() => {
        const previous = previousCallStatusRef.current;
        previousCallStatusRef.current = callStatus;

        if (callStatus === "dialing") {
            startRingingTone();
        } else {
            stopRingingTone();
        }
        if (callStatus === "connected" && previous === "dialing") {
            vibrateOnConnect();
        }
        if (callStatus === "ended" && (previous === "connected" || previous === "dialing")) {
            playHangupBeep();
        }
    }, [callStatus]);

    // Stop any looping tones when leaving the page.
    useEffect(() => stopRingingTone, []);

    const handleVoiceError = useCallback((err: Error) => {
        setError(err.message);
        setTimeout(() => setError(null), 5000);
    }, []);

    const handleVoiceSessionCreated = useCallback((newSessionId: string) => {
        setSessionId(newSessionId);
        setCallStatus("connected");
    }, []);

    const completeSession = useCallback(async (sid: string) => {
        if (isCompleting) return;
        setIsCompleting(true);
        try {
            const sessionFeedback = await completeDialogSession(sid);
            if (sessionFeedback) {
                setFeedback(sessionFeedback);
                queryClient.invalidateQueries({ queryKey: ["profile"] });
            } else {
                // Empty call — nothing was evaluated, no feedback modal.
                setSessionId(null);
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : "Не удалось завершить звонок");
        } finally {
            setIsCompleting(false);
        }
    }, [isCompleting, queryClient]);

    const handleTranscript = useCallback((transcript: string) => {
        assistantReplyOpenRef.current = false;
        setSubtitles((previous) => [...previous, { role: "user", text: transcript }]);
    }, []);

    const handleAiText = useCallback((textChunk: string) => {
        setSubtitles((previous) => {
            const last = previous[previous.length - 1];
            if (assistantReplyOpenRef.current && last?.role === "assistant") {
                return [
                    ...previous.slice(0, -1),
                    { role: "assistant" as const, text: `${last.text} ${textChunk}`.trim() },
                ];
            }
            assistantReplyOpenRef.current = true;
            return [...previous, { role: "assistant", text: textChunk }];
        });
    }, []);

    const handleAiResponse = useCallback((_content: string, isStopSignal: boolean) => {
        assistantReplyOpenRef.current = false;
        if (isStopSignal && sessionId) {
            setCallStatus("ended");
            completeSession(sessionId);
        }
    }, [sessionId, completeSession]);

    const {
        state: voiceState,
        currentTranscript,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    } = useVoice({
        sessionId,
        modeVoiceEnabled: currentMode?.voiceEnabled ?? false,
        bundleId,
        modeId,
        onSessionCreated: handleVoiceSessionCreated,
        onTranscript: handleTranscript,
        onAiText: handleAiText,
        onAiResponse: handleAiResponse,
        onError: handleVoiceError,
    });

    // Barge-in indicator: when the user starts speaking while the AI reply is
    // still playing, mark the last assistant subtitle as interrupted.
    useEffect(() => {
        const previous = previousVoiceStateRef.current;
        previousVoiceStateRef.current = voiceState;

        if (previous === "playing" && voiceState === "speaking") {
            assistantReplyOpenRef.current = false;
            setSubtitles((entries) => {
                const last = entries[entries.length - 1];
                if (last?.role !== "assistant" || last.interrupted) return entries;
                return [...entries.slice(0, -1), { ...last, interrupted: true }];
            });
        }
    }, [voiceState]);

    // Keep the newest subtitle line in view.
    useEffect(() => {
        const container = subtitleScrollRef.current;
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    }, [subtitles, currentTranscript]);

    const handlePickUp = useCallback(async () => {
        if (callStatus !== "idle" && callStatus !== "ended") return;
        setCallStatus("dialing");
        setError(null);
        setSessionTimer(0);
        setFeedback(null);
        setSubtitles([]);
        assistantReplyOpenRef.current = false;
        if (callStatus === "ended") {
            setSessionId(null);
        }
        try {
            await startVoice();
        } catch {
            setCallStatus("idle");
        }
    }, [callStatus, startVoice]);

    const handleHangUp = useCallback(() => {
        if (callStatus !== "connected" && callStatus !== "dialing") return;
        setCallStatus("ended");
        stopVoice();
        refetchUsage();
        if (sessionId) {
            completeSession(sessionId);
        }
    }, [callStatus, stopVoice, refetchUsage, sessionId, completeSession]);

    const handleClose = useCallback(() => {
        stopVoice();
        // Leaving mid-call: end the session cleanly so usage and history
        // are recorded (fire-and-forget — the request outlives navigation).
        if ((callStatus === "connected" || callStatus === "dialing") && sessionId) {
            completeDialogSession(sessionId).catch(() => {});
        }
        router.push(`/dialog/${bundleId}`);
    }, [stopVoice, callStatus, sessionId, router, bundleId]);

    const handleCloseFeedback = useCallback(() => {
        setFeedback(null);
        setSessionId(null);
        setSessionTimer(0);
        setCallStatus("idle");
    }, []);

    const info = describePipeline(voiceState, callStatus);
    const personaSeed = `${currentMode?.id ?? "persona"}-${currentMode?.title ?? ""}`;
    const isCallActive = callStatus === "connected" || callStatus === "dialing";

    if (!isVoiceAvailable && currentMode) {
        return (
            <div style={{ minHeight: "100vh", background: "var(--bg)", display: "flex", alignItems: "center", justifyContent: "center", padding: 40 }}>
                <div style={{ maxWidth: 460, width: "100%", textAlign: "center", background: "var(--surface)", border: "1px solid var(--line)", borderRadius: 24, padding: 40, boxShadow: "var(--sh-1)" }}>
                    <div style={{ width: 72, height: 72, borderRadius: 18, background: "var(--bad-soft)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 20px" }}>
                        <Icon name="mic" size="lg" color="var(--bad)" />
                    </div>
                    <h1 style={{ fontSize: 24, fontWeight: 500, marginBottom: 8 }}>Голосовой режим недоступен</h1>
                    <p style={{ fontSize: 14, color: "var(--ink-3)", marginBottom: 24 }}>
                        Этот сценарий не поддерживает звонки, либо браузер не умеет распознавать речь.
                        Попробуйте Chrome или Edge на десктопе.
                    </p>
                    <button
                        onClick={handleClose}
                        style={{ padding: "12px 24px", borderRadius: 12, background: "var(--ink)", color: "var(--bg)", border: "none", cursor: "pointer", fontWeight: 500 }}
                    >
                        Назад
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div style={{ minHeight: "100vh", background: "var(--bg)", display: "flex", flexDirection: "column" }}>
            {/* Top bar */}
            <header
                style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    padding: "20px 32px",
                    borderBottom: "1px solid var(--line)",
                    background: "var(--surface-2)",
                }}
            >
                <button
                    onClick={handleClose}
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                        background: "transparent",
                        border: "none",
                        cursor: "pointer",
                        color: "var(--ink-3)",
                        fontSize: 13,
                        padding: 0,
                    }}
                >
                    <Icon name="chevron-left" size="sm" />
                    К сценариям
                </button>

                <div
                    style={{
                        fontFamily: "var(--f-mono)",
                        fontSize: 13,
                        color: callStatus === "connected" ? "var(--olive)" : "var(--ink-3)",
                        letterSpacing: 1,
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                    }}
                >
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            background: callStatus === "connected" ? "var(--olive)" : "var(--ink-3)",
                            display: "inline-block",
                            animation: callStatus === "connected" ? "pulse 1.4s ease-in-out infinite" : "none",
                        }}
                    />
                    {callStatus === "idle" && "ОЖИДАНИЕ"}
                    {callStatus === "dialing" && "ВЫЗОВ"}
                    {callStatus === "connected" && formatTime(sessionTimer)}
                    {callStatus === "ended" && "ЗАВЕРШЁН"}
                </div>

                <div style={{ minWidth: 100, display: "flex", justifyContent: "flex-end" }}>
                    {usage && usage.dailyLimitSeconds > 0 && (
                        <div
                            style={{
                                fontFamily: "var(--f-mono)",
                                fontSize: 11,
                                color: usage.dailyExceeded ? "var(--bad)" : "var(--ink-3)",
                                letterSpacing: 1,
                                textAlign: "right",
                                lineHeight: 1.3,
                            }}
                            title={`Сегодня: ${Math.round(usage.dailyUsedSeconds / 60)} / ${Math.round(usage.dailyLimitSeconds / 60)} мин · В месяце: ${Math.round(usage.monthlyUsedSeconds / 60)} / ${Math.round(usage.monthlyLimitSeconds / 60)} мин`}
                        >
                            {Math.round(usage.dailyUsedSeconds / 60)}/{Math.round(usage.dailyLimitSeconds / 60)} МИН
                            <br />
                            <span style={{ opacity: 0.6 }}>СЕГОДНЯ</span>
                        </div>
                    )}
                </div>
            </header>

            {/* Call body */}
            <main
                style={{
                    flex: 1,
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    padding: "40px 24px 60px",
                    gap: 28,
                    position: "relative",
                }}
            >
                {/* Persona block */}
                <div style={{ position: "relative", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    {info.pulse && (
                        <div
                            style={{
                                position: "absolute",
                                inset: -24,
                                borderRadius: "50%",
                                border: `2px solid ${info.ringColor}`,
                                opacity: 0.5,
                                animation: "ping 1.6s cubic-bezier(0, 0, 0.2, 1) infinite",
                            }}
                        />
                    )}
                    <div
                        style={{
                            padding: 8,
                            borderRadius: "50%",
                            background: "var(--surface)",
                            border: `3px solid ${info.ringColor}`,
                            boxShadow: "var(--sh-2)",
                            transition: "border-color 0.3s ease",
                        }}
                    >
                        <GeoAvatar seed={personaSeed} size={168} style={{ borderRadius: "50%" }} />
                    </div>
                </div>

                <div style={{ textAlign: "center", maxWidth: 480 }}>
                    <div style={{ fontSize: 12, color: "var(--ink-3)", letterSpacing: 2, textTransform: "uppercase", marginBottom: 8, fontFamily: "var(--f-mono)" }}>
                        {currentBundle?.title ?? "СЦЕНАРИЙ"}
                    </div>
                    <h1 style={{ fontSize: 32, letterSpacing: -1, fontWeight: 500, lineHeight: 1.1, margin: 0 }}>
                        {currentMode?.title ?? "Собеседник"}
                    </h1>
                    {currentMode?.description && (
                        <p style={{ fontSize: 14, color: "var(--ink-3)", marginTop: 10, lineHeight: 1.5 }}>
                            {currentMode.description}
                        </p>
                    )}
                </div>

                {/* Live subtitles */}
                {(subtitles.length > 0 || (isCallActive && currentTranscript)) && (
                    <div
                        ref={subtitleScrollRef}
                        style={{
                            width: "100%",
                            maxWidth: 560,
                            maxHeight: "26vh",
                            overflowY: "auto",
                            display: "flex",
                            flexDirection: "column",
                            gap: 10,
                            padding: "4px 8px",
                        }}
                    >
                        {subtitles.map((entry, index) => (
                            <div
                                key={index}
                                style={{
                                    alignSelf: entry.role === "user" ? "flex-end" : "flex-start",
                                    maxWidth: "85%",
                                }}
                            >
                                <div
                                    style={{
                                        fontSize: 10,
                                        fontFamily: "var(--f-mono)",
                                        letterSpacing: 1,
                                        textTransform: "uppercase",
                                        color: "var(--ink-3)",
                                        marginBottom: 2,
                                        textAlign: entry.role === "user" ? "right" : "left",
                                    }}
                                >
                                    {entry.role === "user" ? "Вы" : currentMode?.title ?? "Собеседник"}
                                    {entry.interrupted && (
                                        <span style={{ color: "var(--clay)", marginLeft: 6 }}>· прервано</span>
                                    )}
                                </div>
                                <div
                                    style={{
                                        background: entry.role === "user" ? "var(--surface-2)" : "var(--surface)",
                                        border: entry.interrupted ? "1px dashed var(--line-2)" : "1px solid var(--line)",
                                        borderRadius: 12,
                                        padding: "8px 12px",
                                        fontSize: 14,
                                        lineHeight: 1.45,
                                        color: "var(--ink)",
                                        opacity: entry.interrupted ? 0.6 : 1,
                                        transition: "opacity 0.3s ease",
                                    }}
                                >
                                    {entry.text}
                                </div>
                            </div>
                        ))}
                        {/* Interim line: what the recognizer hears right now, before the phrase is committed */}
                        {isCallActive &&
                            currentTranscript &&
                            (voiceState === "speaking" || voiceState === "listening") && (
                                <div style={{ alignSelf: "flex-end", maxWidth: "85%" }}>
                                    <div
                                        style={{
                                            background: "var(--surface-2)",
                                            border: "1px dashed var(--line)",
                                            borderRadius: 12,
                                            padding: "8px 12px",
                                            fontSize: 14,
                                            lineHeight: 1.45,
                                            color: "var(--ink-3)",
                                            fontStyle: "italic",
                                        }}
                                    >
                                        {currentTranscript}
                                    </div>
                                </div>
                            )}
                    </div>
                )}

                {/* State pill */}
                <div
                    style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 8,
                        padding: "10px 18px",
                        borderRadius: 999,
                        background: "var(--surface)",
                        border: "1px solid var(--line)",
                        fontSize: 14,
                        fontWeight: 500,
                        color: "var(--ink)",
                    }}
                >
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            background: info.ringColor,
                            animation: info.pulse ? "pulse 1.2s ease-in-out infinite" : "none",
                        }}
                    />
                    {info.label}
                </div>
                <p style={{ fontSize: 13, color: "var(--ink-3)", textAlign: "center", maxWidth: 380, margin: 0, minHeight: 38 }}>
                    {info.hint}
                </p>

                {error && (
                    <div
                        style={{
                            background: "var(--bad-soft)",
                            color: "var(--bad)",
                            padding: "10px 16px",
                            borderRadius: 12,
                            fontSize: 13,
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                        }}
                    >
                        <Icon name="warning" size="sm" />
                        {error}
                    </div>
                )}

                {isCompleting && (
                    <div style={{ display: "flex", alignItems: "center", gap: 10, color: "var(--ink-3)", fontSize: 13 }}>
                        <span
                            style={{
                                width: 14,
                                height: 14,
                                borderRadius: "50%",
                                border: "2px solid var(--indigo)",
                                borderTopColor: "transparent",
                                animation: "spin 0.8s linear infinite",
                            }}
                        />
                        Готовим разбор...
                    </div>
                )}
            </main>

            {/* Call controls */}
            <footer
                style={{
                    padding: "24px 32px 40px",
                    borderTop: "1px solid var(--line)",
                    background: "var(--surface-2)",
                    display: "flex",
                    justifyContent: "center",
                    gap: 24,
                }}
            >
                {!isCallActive && !feedback && (
                    <button
                        onClick={handlePickUp}
                        disabled={isCompleting}
                        aria-label="Позвонить"
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                            padding: "16px 36px",
                            borderRadius: 999,
                            background: "var(--olive)",
                            color: "var(--bg)",
                            border: "none",
                            cursor: isCompleting ? "not-allowed" : "pointer",
                            fontSize: 16,
                            fontWeight: 600,
                            boxShadow: "var(--sh-2)",
                            opacity: isCompleting ? 0.5 : 1,
                            transition: "transform 0.15s ease",
                        }}
                    >
                        <Icon name="phone" size="md" />
                        {callStatus === "ended" ? "Позвонить ещё раз" : "Позвонить"}
                    </button>
                )}

                {isCallActive && (
                    <button
                        onClick={handleHangUp}
                        aria-label="Положить трубку"
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                            padding: "16px 36px",
                            borderRadius: 999,
                            background: "var(--bad)",
                            color: "#fff",
                            border: "none",
                            cursor: "pointer",
                            fontSize: 16,
                            fontWeight: 600,
                            boxShadow: "var(--sh-2)",
                        }}
                    >
                        <Icon name="phone" size="md" style={{ transform: "rotate(135deg)" }} />
                        Положить трубку
                    </button>
                )}

                {feedback && (
                    <button
                        onClick={handleCloseFeedback}
                        style={{
                            padding: "16px 36px",
                            borderRadius: 999,
                            background: "var(--indigo)",
                            color: "var(--bg)",
                            border: "none",
                            cursor: "pointer",
                            fontSize: 16,
                            fontWeight: 600,
                            boxShadow: "var(--sh-2)",
                        }}
                    >
                        Закрыть разбор
                    </button>
                )}
            </footer>

            {feedback && <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />}

            <style jsx>{`
                @keyframes pulse {
                    0%, 100% { opacity: 1; transform: scale(1); }
                    50% { opacity: 0.55; transform: scale(0.85); }
                }
                @keyframes ping {
                    0% { transform: scale(1); opacity: 0.7; }
                    80%, 100% { transform: scale(1.4); opacity: 0; }
                }
                @keyframes spin {
                    to { transform: rotate(360deg); }
                }
            `}</style>
        </div>
    );
}
