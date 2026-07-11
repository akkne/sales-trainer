"use client";

import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useState, useEffect, useRef, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useCompany } from "@/features/companies/hooks/use-companies";
import { useCompanyCallMode } from "@/features/companies/hooks/use-company-call-mode";
import { useCreatePracticeCall } from "@/features/companies/hooks/use-practice-calls";
import {
    DialogMessage,
    DialogFeedback,
    startDialogSession,
    sendDialogMessage,
    completeDialogSession,
} from "@/features/dialog/hooks/use-dialog";
import { ChatMessage } from "@/features/dialog/components/chat-message";
import { ChatInput } from "@/features/dialog/components/chat-input";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import { Icon } from "@/shared/components/icon";
import { toast } from "@/features/notifications/store/toast-store";
import { ApiError } from "@/shared/api/api-client";

const COMPANY_CONTEXT_GOAL_MAX = 500;

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

export default function CompanyChatCallPage() {
    const params = useParams<{ id: string }>();
    const router = useRouter();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const companyId = params.id;

    const { data: company, isLoading: isCompanyLoading, error: companyError } = useCompany(companyId);
    const { data: callMode, isLoading: isCallModeLoading, error: callModeError } = useCompanyCallMode();
    const createPracticeCall = useCreatePracticeCall(companyId);

    const [goal] = useState(() => readStoredGoal(companyId, searchParams.get("goal")));
    const [persona] = useState(() => readStoredPersona(companyId));

    const [sessionId, setSessionId] = useState<string | null>(null);
    const [messages, setMessages] = useState<DialogMessage[]>([]);
    const [isStarting, setIsStarting] = useState(false);
    const [isSending, setIsSending] = useState(false);
    const [isCompleting, setIsCompleting] = useState(false);
    const [isEnded, setIsEnded] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<DialogFeedback | null>(null);

    const messagesEndRef = useRef<HTMLDivElement>(null);
    const startedRef = useRef(false);

    const isCompanyNotFound = companyError instanceof ApiError && companyError.status === 404;

    useEffect(() => {
        if (isCompanyNotFound) {
            toast.error("Компания не найдена");
            router.replace("/companies");
        }
    }, [isCompanyNotFound, router]);

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);

    const handleClose = useCallback(() => {
        router.push(`/companies/${companyId}`);
    }, [router, companyId]);

    const startSession = useCallback(async () => {
        if (!company || !callMode) return;
        setIsStarting(true);
        setError(null);
        try {
            const session = await startDialogSession(callMode.bundleId, callMode.modeId, {
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
            });
            setSessionId(session.id);
            setMessages(session.messages);
            createPracticeCall.mutate({ dialogSessionId: session.id, goal });
        } catch (sessionError) {
            setError(sessionError instanceof Error ? sessionError.message : "Не удалось начать сессию");
        } finally {
            setIsStarting(false);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [company, callMode, goal]);

    useEffect(() => {
        if (startedRef.current) return;
        if (!company || !callMode) return;
        startedRef.current = true;
        startSession();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [company, callMode]);

    const autoCompleteSession = useCallback(async (sid: string) => {
        if (isCompleting) return;
        setIsCompleting(true);
        setError(null);
        try {
            const sessionFeedback = await completeDialogSession(sid);
            if (sessionFeedback) {
                setFeedback(sessionFeedback);
                queryClient.invalidateQueries({ queryKey: ["profile"] });
            }
        } catch (completeError) {
            setError(completeError instanceof Error ? completeError.message : "Не удалось завершить сессию");
        } finally {
            setIsCompleting(false);
        }
    }, [isCompleting, queryClient]);

    const handleSendMessage = async (content: string) => {
        if (!sessionId || isSending) return;

        const userMessage: DialogMessage = {
            role: "user",
            content,
            timestamp: new Date().toISOString(),
            isStopSignal: false,
        };
        setMessages((previous) => [...previous, userMessage]);
        setIsSending(true);
        setError(null);

        try {
            const aiMessage = await sendDialogMessage(sessionId, content);
            setMessages((previous) => [...previous, aiMessage]);
            if (aiMessage.isStopSignal) {
                setIsEnded(true);
                autoCompleteSession(sessionId);
            }
        } catch (sendError) {
            setError(sendError instanceof Error ? sendError.message : "Не удалось отправить сообщение");
        } finally {
            setIsSending(false);
        }
    };

    const handleEndSession = () => {
        if (sessionId && !isEnded) {
            setIsEnded(true);
            autoCompleteSession(sessionId);
        }
    };

    const handleCloseFeedback = () => {
        setFeedback(null);
        router.push(`/companies/${companyId}`);
    };

    if (isCompanyLoading || isCallModeLoading || isCompanyNotFound) {
        return (
            <div className="chat-screen" style={{ gridTemplateColumns: "1fr" }}>
                <main className="dc-main">
                    <div className="row center grow">
                        <div style={{ width: 36, height: 36, borderRadius: "50%", border: "3px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
                    </div>
                </main>
            </div>
        );
    }

    if (companyError && !isCompanyNotFound) {
        return (
            <div className="chat-screen" style={{ gridTemplateColumns: "1fr" }}>
                <main className="dc-main">
                    <div className="col center grow" style={{ padding: 16 }}>
                        <div className="empty" style={{ padding: "20px 0 0" }}>
                            <div className="ic" style={{ background: "var(--heart-soft)", color: "var(--heart)" }}>
                                <Icon name="warning" size="xl" />
                            </div>
                            <p className="body" style={{ color: "var(--heart)", fontWeight: 600, marginBottom: 20 }}>
                                {companyError.message}
                            </p>
                            <Link href="/companies" className="btn btn-outline">← К списку</Link>
                        </div>
                    </div>
                </main>
            </div>
        );
    }

    if (callModeError) {
        return (
            <div className="chat-screen" style={{ gridTemplateColumns: "1fr" }}>
                <main className="dc-main">
                    <div className="col center grow" style={{ padding: 16 }}>
                        <div className="empty" style={{ padding: "20px 0 0" }}>
                            <div className="ic" style={{ background: "var(--heart-soft)", color: "var(--heart)" }}>
                                <Icon name="warning" size="xl" />
                            </div>
                            <p className="body" style={{ color: "var(--heart)", fontWeight: 600, marginBottom: 8 }}>
                                Тренировочные звонки недоступны
                            </p>
                            <p className="small" style={{ marginBottom: 20 }}>
                                ИИ для звонков компаниям сейчас не настроен. Попробуйте позже.
                            </p>
                            <Link href={`/companies/${companyId}`} className="btn btn-outline">← К компании</Link>
                        </div>
                    </div>
                </main>
            </div>
        );
    }

    return (
        <div className="chat-screen" style={{ gridTemplateColumns: "1fr" }}>
            <main className="dc-main">
                <div className="dc-head">
                    <span className="itile primary" style={{ width: 36, height: 36, borderRadius: "50%", flex: "none" }}>
                        <Icon name="sparkle" size={18} />
                    </span>
                    <div style={{ minWidth: 0, flex: 1 }}>
                        <div className="dc-head-title">{company?.name ?? "Компания"}</div>
                        <div className="dc-head-sub">{goal || "Тренировочный звонок"} · текст</div>
                    </div>

                    {sessionId && !isEnded && !feedback && (
                        <button
                            className="btn btn-sm"
                            onClick={handleEndSession}
                            style={{ background: "var(--heart-soft)", color: "var(--heart)", flex: "none", borderColor: "transparent" }}
                        >
                            <Icon name="close" size="sm" />
                            Завершить
                        </button>
                    )}

                    <button className="icon-btn" onClick={handleClose} aria-label="Закрыть" style={{ flex: "none" }}>
                        <Icon name="close" size="md" />
                    </button>
                </div>

                {(isSending || isCompleting || isStarting) && (
                    <div className="dc-status-bar" role="status" aria-live="polite">
                        <span style={{ width: 14, height: 14, border: "2px solid var(--primary)", borderTopColor: "transparent", borderRadius: "50%", animation: "spin 0.8s linear infinite", display: "inline-block", flex: "none" }} />
                        {isCompleting ? "Готовим разбор…" : isStarting ? "Готовим сессию…" : "ИИ печатает…"}
                    </div>
                )}

                <div className="dc-thread">
                    <div className="dc-thread-inner">
                        {messages.map((message, messageIndex) => (
                            <ChatMessage key={messageIndex} message={message} />
                        ))}

                        {error && (
                            <div className="row center">
                                <span className="badge" style={{ background: "var(--heart-soft)", color: "var(--heart)", padding: "8px 16px", fontSize: 13, borderRadius: "var(--r-xs)" }}>
                                    <Icon name="warning" size="sm" />
                                    {error}
                                </span>
                            </div>
                        )}

                        <div ref={messagesEndRef} />
                    </div>
                </div>

                <div className="dc-input">
                    <ChatInput
                        onSend={handleSendMessage}
                        disabled={isSending || isCompleting || isEnded || !!feedback || !sessionId}
                        placeholder={isEnded || feedback ? "Диалог завершён" : "Введите сообщение…"}
                    />
                </div>
            </main>

            {feedback && <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />}
        </div>
    );
}
