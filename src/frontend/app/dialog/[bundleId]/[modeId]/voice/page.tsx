"use client";

import { useParams, useRouter } from "next/navigation";
import { useState, useEffect, useCallback, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
    useDialogBundles,
    useDialogModes,
    completeDialogSession,
    DialogFeedback,
} from "@/features/dialog/hooks/use-dialog";
import { useVoice, VoicePipelineState } from "@/features/voice/hooks/use-voice";
import { useVoiceUsage } from "@/features/voice/hooks/use-voice-usage";
import { TimingConstants } from "@/shared/constants/timing-constants";
import { CallSoundsPlayer } from "@/features/voice/services/call-sounds-player";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";

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
            hint: "Нажми «Звонок», чтобы связаться с собеседником",
            ringColor: "var(--line-strong)",
            pulse: false,
        };
    }
    if (callStatus === "dialing" || state === "initializing") {
        return {
            label: "Соединение…",
            hint: "Звоним. Собеседник вот-вот возьмёт трубку",
            ringColor: "var(--primary)",
            pulse: true,
        };
    }
    if (callStatus === "ended") {
        return {
            label: "Звонок завершён",
            hint: "Готовим разбор…",
            ringColor: "var(--line-strong)",
            pulse: false,
        };
    }
    switch (state) {
        case "listening":
            return {
                label: "На связи · слушаю",
                hint: "Говори свободно — отвечу, как только сделаешь паузу",
                ringColor: "var(--success)",
                pulse: false,
            };
        case "speaking":
            return {
                label: "Слышу тебя",
                hint: "Продолжай — записываю твою реплику",
                ringColor: "var(--success)",
                pulse: true,
            };
        case "processing":
            return {
                label: "Думаю…",
                hint: "Готовлю реплику собеседника",
                ringColor: "var(--amber)",
                pulse: true,
            };
        case "playing":
            return {
                label: "Собеседник говорит",
                hint: "Перебей в любой момент, чтобы ответить",
                ringColor: "var(--flame)",
                pulse: true,
            };
        case "error":
            return {
                label: "Проблема со связью",
                hint: "Попробуй позвонить снова",
                ringColor: "var(--heart)",
                pulse: false,
            };
        default:
            return {
                label: "На линии",
                hint: "",
                ringColor: "var(--primary)",
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
    const callSoundsPlayerRef = useRef<CallSoundsPlayer>(new CallSoundsPlayer());
    const previousCallStatusRef = useRef<CallStatus>("idle");
    const previousVoiceStateRef = useRef<VoicePipelineState>("idle");
    const assistantReplyOpenRef = useRef<boolean>(false);
    const subtitleScrollRef = useRef<HTMLDivElement>(null);

    useEffect(() => () => callSoundsPlayerRef.current.stopRinging(), []);
    useEffect(() => {
        if (callStatus === "connected") {
            timerRef.current = setInterval(() => {
                setSessionTimer((prev) => prev + 1);
            }, TimingConstants.oneSecondMs);
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

    useEffect(() => {
        const previous = previousCallStatusRef.current;
        previousCallStatusRef.current = callStatus;

        if (callStatus === "dialing") {
            callSoundsPlayerRef.current.startRinging();
        } else {
            callSoundsPlayerRef.current.stopRinging();
        }
        if (callStatus === "connected" && previous === "dialing") {
            callSoundsPlayerRef.current.vibrateOnConnect();
        }
        if (callStatus === "ended" && (previous === "connected" || previous === "dialing")) {
            callSoundsPlayerRef.current.playHangupBeep();
        }
    }, [callStatus]);

    const handleVoiceError = useCallback((error: Error) => {
        setError(error.message);
        setTimeout(() => setError(null), TimingConstants.fiveSecondsMs);
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
                setSessionId(null);
            }
        } catch (error) {
            setError(error instanceof Error ? error.message : "Не удалось завершить звонок");
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
            <div className="voice">
                <div className="row center grow" style={{ padding: 40 }}>
                    <div className="card fade-up" style={{ maxWidth: 460, width: "100%", textAlign: "center", padding: 40 }}>
                        <span className="itile heart" style={{ width: 72, height: 72, margin: "0 auto 20px" }}>
                            <Icon name="mic" size="lg" />
                        </span>
                        <h1 className="h3" style={{ marginBottom: 8 }}>Голосовой режим недоступен</h1>
                        <p className="small" style={{ marginBottom: 24 }}>
                            Этот сценарий не поддерживает звонки, либо твой браузер не распознаёт речь.
                            Попробуй Chrome или Edge на компьютере.
                        </p>
                        <button className="btn btn-dark" onClick={handleClose}>
                            Назад
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="voice">
            {/* Top bar */}
            <div className="voice-top">
                <button className="back-link plain" onClick={handleClose} aria-label="Назад к сценариям">
                    <Icon name="chevron-left" size="sm" />
                    Сценарии
                </button>

                {/* Center: status dot + mono status label / live timer */}
                <div
                    className="voice-status"
                    style={{ color: callStatus === "connected" ? "var(--success)" : "var(--ink-2)" }}
                >
                    <span
                        className={"vdot" + (callStatus === "connected" ? " live" : "")}
                        style={{ background: callStatus === "connected" ? "var(--success)" : "var(--ink-4)" }}
                    />
                    <span className="num">
                        {callStatus === "idle"      && "ОЖИДАНИЕ"}
                        {callStatus === "dialing"   && "ЗВОНИМ"}
                        {callStatus === "connected" && formatTime(sessionTimer)}
                        {callStatus === "ended"     && "ЗАВЕРШЁН"}
                    </span>
                </div>

                {/* Right: voice quota */}
                <div style={{ minWidth: 100, display: "flex", justifyContent: "flex-end" }}>
                    {usage && usage.dailyLimitSeconds > 0 && (
                        <div
                            className={"voice-quota num" + (usage.dailyExceeded ? " exceeded" : "")}
                            title={`Сегодня: ${Math.round(usage.dailyUsedSeconds / 60)} / ${Math.round(usage.dailyLimitSeconds / 60)} мин · За месяц: ${Math.round(usage.monthlyUsedSeconds / 60)} / ${Math.round(usage.monthlyLimitSeconds / 60)} мин`}
                        >
                            {Math.round(usage.dailyUsedSeconds / 60)}/{Math.round(usage.dailyLimitSeconds / 60)} МИН
                        </div>
                    )}
                </div>
            </div>

            {/* Call stage */}
            <div className="voice-stage">
                {/* Avatar + state ring — ring color and pulse driven by describePipeline() */}
                <div
                    className={"voice-avatar" + (info.pulse ? " pulse" : "")}
                    style={{ "--ring": info.ringColor } as React.CSSProperties}
                >
                    <div className="va-ring" />
                    <GeoAvatar seed={personaSeed} size={156} style={{ borderRadius: "50%" }} />
                </div>

                <span className="eyebrow">{currentBundle?.title ?? "Сценарий"}</span>
                <h1 className="h1" style={{ margin: "10px 0 6px", fontSize: "clamp(26px, 3.6vw, 42px)" }}>
                    {currentMode?.title ?? "Собеседник"}
                </h1>
                {currentMode?.description && (
                    <p className="lead" style={{ maxWidth: 480 }}>{currentMode.description}</p>
                )}

                {/* Live transcript bubbles */}
                {(subtitles.length > 0 || (isCallActive && currentTranscript)) && (
                    <div className="transcript" ref={subtitleScrollRef}>
                        {subtitles.map((entry, index) => (
                            <div
                                key={index}
                                className={
                                    "tr-bubble " +
                                    (entry.role === "user" ? "user" : "ai") +
                                    (entry.interrupted ? " interrupted" : "")
                                }
                            >
                                <span className="tr-role">
                                    {entry.role === "user" ? "Ты" : (currentMode?.title ?? "Собеседник")}
                                    {entry.interrupted && (
                                        <span style={{ color: "var(--amber)", marginLeft: 6 }}>· перебито</span>
                                    )}
                                </span>
                                <p>{entry.text}</p>
                            </div>
                        ))}
                        {/* Interim line — what the recognizer hears before the phrase is committed */}
                        {isCallActive && currentTranscript && (voiceState === "speaking" || voiceState === "listening") && (
                            <div className="tr-bubble user interim">
                                <span className="tr-role">Ты</span>
                                <p>{currentTranscript}</p>
                            </div>
                        )}
                    </div>
                )}

                {/* State pill */}
                <div className="state-pill" style={{ marginTop: subtitles.length > 0 ? 0 : 24 }}>
                    <span
                        className={"pdot" + (info.pulse ? " live" : "")}
                        style={{ background: info.ringColor }}
                    />
                    {info.label}
                </div>
                <p className="voice-hint" style={{ minHeight: 38 }}>{info.hint}</p>

                {/* Error alert */}
                {error && (
                    <span
                        className="badge"
                        role="alert"
                        style={{ background: "var(--heart-soft)", color: "var(--heart)", padding: "10px 16px", fontSize: 13, borderRadius: "var(--r-xs)", display: "inline-flex", alignItems: "center", gap: 6 }}
                    >
                        <Icon name="warning" size="sm" />
                        {error}
                    </span>
                )}

                {/* Completing spinner */}
                {isCompleting && (
                    <div className="row gap-2 small" style={{ marginTop: 10, color: "var(--ink-3)" }}>
                        <span
                            style={{
                                width: 14, height: 14, borderRadius: "50%",
                                border: "2px solid var(--primary)", borderTopColor: "transparent",
                                animation: "spin 0.8s linear infinite", display: "inline-block", flex: "none",
                            }}
                        />
                        Готовим разбор…
                    </div>
                )}
            </div>

            {/* Call controls footer */}
            <div className="voice-foot">
                {!isCallActive && !feedback && (
                    <button
                        className="btn btn-success btn-lg voice-cta"
                        onClick={handlePickUp}
                        disabled={isCompleting}
                        aria-label="Позвонить"
                        style={isCompleting ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
                    >
                        <Icon name="phone" size="md" />
                        {callStatus === "ended" ? "Позвонить снова" : "Позвонить"}
                    </button>
                )}

                {isCallActive && (
                    <button className="btn btn-danger btn-lg voice-cta" onClick={handleHangUp} aria-label="Завершить звонок">
                        <Icon name="phone" size="md" style={{ transform: "rotate(135deg)" }} />
                        Завершить
                    </button>
                )}

                {feedback && (
                    <button className="btn btn-primary btn-lg voice-cta" onClick={handleCloseFeedback}>
                        Закрыть разбор
                    </button>
                )}
            </div>

            {feedback && <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />}
        </div>
    );
}
