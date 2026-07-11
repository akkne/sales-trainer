"use client";

import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useState, useEffect, useCallback, useMemo, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useCompany } from "@/features/companies/hooks/use-companies";
import { useCompanyCallMode } from "@/features/companies/hooks/use-company-call-mode";
import { useCreatePracticeCall } from "@/features/companies/hooks/use-practice-calls";
import { completeDialogSession, DialogFeedback } from "@/features/dialog/hooks/use-dialog";
import { useVoice, VoicePipelineState } from "@/features/voice/hooks/use-voice";
import { useVoiceUsage } from "@/features/voice/hooks/use-voice-usage";
import { TimingConstants } from "@/shared/constants/timing-constants";
import { CallSoundsPlayer } from "@/features/voice/services/call-sounds-player";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { toast } from "@/features/notifications/store/toast-store";
import { ApiError } from "@/shared/api/api-client";

const COMPANY_CONTEXT_GOAL_MAX = 500;

function formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
}

function readStoredGoal(companyId: string, queryGoal: string | null): string {
    if (typeof window === "undefined") return queryGoal ?? "";
    const stored = window.sessionStorage.getItem(`company-call-goal:${companyId}`);
    return stored !== null ? stored : (queryGoal ?? "");
}

interface StoredPersona {
    name: string;
    position: string;
    personality: string;
    difficulty: string;
}

function readStoredPersona(companyId: string): StoredPersona | null {
    if (typeof window === "undefined") return null;
    const stored = window.sessionStorage.getItem(`company-call-persona:${companyId}`);
    if (!stored) return null;
    try {
        return JSON.parse(stored) as StoredPersona;
    } catch {
        return null;
    }
}

type CallStatus = "idle" | "dialing" | "connected" | "ended";

interface SubtitleEntry {
    role: "user" | "assistant";
    text: string;
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
            hint: "Нажмите «Позвонить», чтобы связаться с собеседником",
            ringColor: "var(--line-strong)",
            pulse: false,
        };
    }
    if (callStatus === "dialing" || state === "initializing") {
        return {
            label: "Соединение…",
            hint: "Идёт вызов. Собеседник вот-вот ответит",
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
                hint: "Говорите свободно — отвечу, как только вы сделаете паузу",
                ringColor: "var(--success)",
                pulse: false,
            };
        case "speaking":
            return {
                label: "Слышу вас",
                hint: "Продолжайте — записываю реплику",
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
                hint: "Прервите в любой момент, чтобы ответить",
                ringColor: "var(--flame)",
                pulse: true,
            };
        case "error":
            return {
                label: "Проблема со связью",
                hint: "Попробуйте позвонить снова",
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

export default function CompanyVoiceCallPage() {
    const params = useParams<{ id: string }>();
    const router = useRouter();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const companyId = params.id;

    const { data: company, isLoading: isCompanyLoading, error: companyError } = useCompany(companyId);
    const { data: callMode, isLoading: isCallModeLoading, error: callModeError } = useCompanyCallMode();
    const { data: usage, refetch: refetchUsage } = useVoiceUsage();
    const createPracticeCall = useCreatePracticeCall(companyId);

    const [goal] = useState(() => readStoredGoal(companyId, searchParams.get("goal")));
    const [persona] = useState(() => readStoredPersona(companyId));

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
    const callEndedRef = useRef(false);
    const previousVoiceStateRef = useRef<VoicePipelineState>("idle");
    const assistantReplyOpenRef = useRef<boolean>(false);
    const subtitleScrollRef = useRef<HTMLDivElement>(null);

    const isCompanyNotFound = companyError instanceof ApiError && companyError.status === 404;

    useEffect(() => {
        if (isCompanyNotFound) {
            toast.error("Компания не найдена");
            router.replace("/companies");
        }
    }, [isCompanyNotFound, router]);

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
        if (callEndedRef.current) return;
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
                createPracticeCall.mutate({ dialogSessionId: sid, goal });
                queryClient.invalidateQueries({ queryKey: ["profile"] });
            } else {
                setSessionId(null);
            }
        } catch (error) {
            setError(error instanceof Error ? error.message : "Не удалось завершить звонок");
        } finally {
            setIsCompleting(false);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isCompleting, queryClient, goal]);

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

    const companyContext = useMemo(
        () =>
            company
                ? {
                      companyName: company.name,
                      companyDescription: company.description,
                      ...(goal ? { callGoal: goal.slice(0, COMPANY_CONTEXT_GOAL_MAX) } : {}),
                      ...(persona
                          ? {
                                personaName: persona.name,
                                personaPosition: persona.position,
                                personaPersonality: persona.personality,
                                personaDifficulty: persona.difficulty,
                            }
                          : {}),
                  }
                : undefined,
        [company, goal, persona],
    );

    const {
        state: voiceState,
        currentTranscript,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    } = useVoice({
        sessionId,
        modeVoiceEnabled: true,
        bundleId: callMode?.bundleId,
        modeId: callMode?.modeId,
        companyContext,
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

    const isReadyToCall = !!company && !!callMode;

    const handlePickUp = useCallback(async () => {
        if (!isReadyToCall) return;
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
    }, [isReadyToCall, callStatus, startVoice]);

    const handleHangUp = useCallback(() => {
        if (callStatus !== "connected" && callStatus !== "dialing") return;
        callEndedRef.current = true;
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
        router.push(`/companies/${companyId}`);
    }, [stopVoice, callStatus, sessionId, router, companyId]);

    const handleCloseFeedback = useCallback(() => {
        setFeedback(null);
        setSessionId(null);
        setSessionTimer(0);
        setCallStatus("idle");
        router.push(`/companies/${companyId}`);
    }, [router, companyId]);

    const info = describePipeline(voiceState, callStatus);
    const personaSeed = `company-${companyId}`;
    const isCallActive = callStatus === "connected" || callStatus === "dialing";

    if (isCompanyLoading || isCallModeLoading || isCompanyNotFound) {
        return (
            <div className="voice">
                <div className="row center grow" style={{ padding: 40 }}>
                    <div
                        style={{
                            width: 36, height: 36, borderRadius: "50%",
                            border: "3px solid var(--primary)", borderTopColor: "transparent",
                            animation: "spin 0.8s linear infinite",
                        }}
                    />
                </div>
            </div>
        );
    }

    if (companyError && !isCompanyNotFound) {
        return (
            <div className="voice">
                <div className="row center grow" style={{ padding: 40 }}>
                    <div className="card fade-up" style={{ maxWidth: 460, width: "100%", textAlign: "center", padding: 40 }}>
                        <span className="itile heart" style={{ width: 72, height: 72, margin: "0 auto 20px" }}>
                            <Icon name="warning" size="lg" />
                        </span>
                        <h1 className="h3" style={{ marginBottom: 8 }}>Не удалось загрузить компанию</h1>
                        <p className="small" style={{ marginBottom: 24 }}>{companyError.message}</p>
                        <Link href="/companies" className="btn btn-dark">← К списку</Link>
                    </div>
                </div>
            </div>
        );
    }

    if (callModeError) {
        return (
            <div className="voice">
                <div className="row center grow" style={{ padding: 40 }}>
                    <div className="card fade-up" style={{ maxWidth: 460, width: "100%", textAlign: "center", padding: 40 }}>
                        <span className="itile heart" style={{ width: 72, height: 72, margin: "0 auto 20px" }}>
                            <Icon name="warning" size="lg" />
                        </span>
                        <h1 className="h3" style={{ marginBottom: 8 }}>Тренировочные звонки недоступны</h1>
                        <p className="small" style={{ marginBottom: 24 }}>
                            ИИ для звонков компаниям сейчас не настроен. Попробуйте позже.
                        </p>
                        <Link href={`/companies/${companyId}`} className="btn btn-dark">← К компании</Link>
                    </div>
                </div>
            </div>
        );
    }

    if (!isVoiceAvailable) {
        return (
            <div className="voice">
                <div className="row center grow" style={{ padding: 40 }}>
                    <div className="card fade-up" style={{ maxWidth: 460, width: "100%", textAlign: "center", padding: 40 }}>
                        <span className="itile heart" style={{ width: 72, height: 72, margin: "0 auto 20px" }}>
                            <Icon name="mic" size="lg" />
                        </span>
                        <h1 className="h3" style={{ marginBottom: 8 }}>Голосовой режим недоступен</h1>
                        <p className="small" style={{ marginBottom: 24 }}>
                            Браузер не поддерживает распознавание речи. Попробуйте Chrome или Edge на компьютере.
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
                <button className="back-link plain" onClick={handleClose} aria-label="Назад к компании">
                    <Icon name="chevron-left" size="sm" />
                    {company?.name ?? "Компания"}
                </button>

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

                <div style={{ minWidth: 100, display: "flex", justifyContent: "flex-end" }}>
                    {usage && usage.dailyLimitSeconds > 0 && (
                        <div
                            className={"voice-quota num" + (usage.dailyExceeded ? " exceeded" : "")}
                            title={`Сегодня: ${Math.round(usage.dailyUsedSeconds / 60)} / ${Math.round(usage.dailyLimitSeconds / 60)} мин · В этом месяце: ${Math.round(usage.monthlyUsedSeconds / 60)} / ${Math.round(usage.monthlyLimitSeconds / 60)} мин`}
                        >
                            {Math.round(usage.dailyUsedSeconds / 60)}/{Math.round(usage.dailyLimitSeconds / 60)} МИН
                        </div>
                    )}
                </div>
            </div>

            {/* Call stage */}
            <div className="voice-stage">
                <div
                    className={"voice-avatar" + (info.pulse ? " pulse" : "")}
                    style={{ "--ring": info.ringColor } as React.CSSProperties}
                >
                    <div className="va-ring" />
                    <GeoAvatar seed={personaSeed} size={156} style={{ borderRadius: "50%" }} />
                </div>

                <span className="eyebrow">Тренировочный звонок</span>
                <h1 className="h1" style={{ margin: "10px 0 6px", fontSize: "clamp(26px, 3.6vw, 42px)" }}>
                    {company?.name ?? "Компания"}
                </h1>
                {goal && (
                    <p className="lead" style={{ maxWidth: 480 }}>{goal}</p>
                )}

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
                                    {entry.role === "user" ? "Вы" : (company?.name ?? "Собеседник")}
                                    {entry.interrupted && (
                                        <span style={{ color: "var(--amber)", marginLeft: 6 }}>· прервано</span>
                                    )}
                                </span>
                                <p>{entry.text}</p>
                            </div>
                        ))}
                        {isCallActive && currentTranscript && (voiceState === "speaking" || voiceState === "listening") && (
                            <div className="tr-bubble user interim">
                                <span className="tr-role">Вы</span>
                                <p>{currentTranscript}</p>
                            </div>
                        )}
                    </div>
                )}

                <div className="state-pill" style={{ marginTop: subtitles.length > 0 ? 0 : 24 }}>
                    <span
                        className={"pdot" + (info.pulse ? " live" : "")}
                        style={{ background: info.ringColor }}
                    />
                    {info.label}
                </div>
                <p className="voice-hint" style={{ minHeight: 38 }}>{info.hint}</p>

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
                        disabled={isCompleting || !isReadyToCall}
                        aria-label="Позвонить"
                        style={isCompleting || !isReadyToCall ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
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
