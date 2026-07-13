"use client";

import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useState, useEffect, useRef, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
    useDialogBundles,
    useDialogModes,
    useDialogSessions,
    DialogMessage,
    DialogFeedback,
    startDialogSession,
    sendDialogMessage,
    completeDialogSession,
    deleteDialogSession,
} from "@/features/dialog/hooks/use-dialog";
import { useVoice } from "@/features/voice/hooks/use-voice";
import { apiClient } from "@/shared/api/api-client";
import { TimingConstants } from "@/shared/constants/timing-constants";
import { ChatMessage } from "@/features/dialog/components/chat-message";
import { ChatInput } from "@/features/dialog/components/chat-input";
import { FeedbackModal } from "@/features/dialog/components/feedback-modal";
import { SessionHistorySidebar } from "@/features/dialog/components/session-history-sidebar";
import { VoiceMicButton } from "@/features/voice/components/voice-mic-button";
import { Icon } from "@/shared/components/icon";

function formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${mins.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
}

export default function ChatPage() {
    const params = useParams();
    const router = useRouter();
    const searchParams = useSearchParams();
    const queryClient = useQueryClient();
    const bundleId = params.bundleId as string;
    const modeId = params.modeId as string;
    const chatMode = searchParams.get("mode") || "text";

    const { data: bundles } = useDialogBundles();
    const { data: modes } = useDialogModes(bundleId);
    const { data: allSessions, refetch: refetchSessions } = useDialogSessions();

    const currentBundle = bundles?.find((bundle) => bundle.id === bundleId);
    const currentMode = modes?.find((mode) => mode.id === modeId);

    const [sessionId, setSessionId] = useState<string | null>(null);
    const [messages, setMessages] = useState<DialogMessage[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isSending, setIsSending] = useState(false);
    const [isCompleting, setIsCompleting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [feedback, setFeedback] = useState<DialogFeedback | null>(null);
    const [isEnded, setIsEnded] = useState(false);
    const [showSidebar, setShowSidebar] = useState(true);
    const [isInitialized, setIsInitialized] = useState(false);
    const [voiceError, setVoiceError] = useState<string | null>(null);
    const [sessionTimer, setSessionTimer] = useState(0);

    const messagesEndRef = useRef<HTMLDivElement>(null);
    const timerRef = useRef<NodeJS.Timeout | null>(null);

    useEffect(() => {
        if (sessionId && !isEnded && !feedback) {
            timerRef.current = setInterval(() => {
                setSessionTimer((prev) => prev + 1);
            }, TimingConstants.oneSecondMs);
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

    useEffect(() => {
        if (sessionId) {
            setSessionTimer(0);
        }
    }, [sessionId]);

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    };

    useEffect(() => {
        scrollToBottom();
    }, [messages]);

    const filteredSessions = allSessions?.filter(
        (session) => session.bundleId === bundleId && session.modeId === modeId
    ) ?? [];

    const initializeNewSession = async () => {
        try {
            setIsLoading(true);
            setError(null);
            setFeedback(null);
            setIsEnded(false);
            const session = await startDialogSession(bundleId, modeId);
            setSessionId(session.id);
            setMessages(session.messages);
            refetchSessions();

            if (session.messages.some((message) => message.isStopSignal)) {
                autoCompleteSession(session.id);
            }
        } catch (sessionError) {
            setError(sessionError instanceof Error ? sessionError.message : "Ошибка запуска сессии");
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        if (isInitialized || allSessions === undefined) return;

        const existingSessions = allSessions.filter(
            (session) => session.bundleId === bundleId && session.modeId === modeId
        );

        if (existingSessions.length === 0) {
            initializeNewSession();
        } else {
            setIsLoading(false);
        }
        setIsInitialized(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [bundleId, modeId, allSessions, isInitialized]);

    const handleSendMessage = async (content: string) => {
        if (isSending) return;

        let currentSessionId = sessionId;

        if (!currentSessionId) {
            try {
                setIsLoading(true);
                setError(null);
                const session = await startDialogSession(bundleId, modeId);
                currentSessionId = session.id;
                setSessionId(session.id);
                setMessages([]);
                refetchSessions();
            } catch (sessionError) {
                setError(sessionError instanceof Error ? sessionError.message : "Ошибка запуска сессии");
                setIsLoading(false);
                return;
            } finally {
                setIsLoading(false);
            }
        }

        const userMessage: DialogMessage = {
            role: "user",
            content,
            timestamp: new Date().toISOString(),
            isStopSignal: false,
        };

        setMessages((previousMessages) => [...previousMessages, userMessage]);
        setIsSending(true);
        setError(null);

        try {
            const aiMessage = await sendDialogMessage(currentSessionId, content);
            setMessages((previousMessages) => [...previousMessages, aiMessage]);

            if (aiMessage.isStopSignal) {
                setIsEnded(true);
                autoCompleteSession(currentSessionId);
            }
        } catch (sendError) {
            setError(sendError instanceof Error ? sendError.message : "Ошибка отправки");
        } finally {
            setIsSending(false);
        }
    };

    const autoCompleteSession = async (sid: string) => {
        if (isCompleting) return;
        setIsCompleting(true);
        setError(null);
        try {
            const sessionFeedback = await completeDialogSession(sid);
            setFeedback(sessionFeedback);
            refetchSessions();
            queryClient.invalidateQueries({ queryKey: ["profile"] });
        } catch (completeError) {
            setError(completeError instanceof Error ? completeError.message : "Ошибка завершения");
        } finally {
            setIsCompleting(false);
        }
    };

    const handleCloseFeedback = () => {
        setFeedback(null);
        if (!isSessionCompleted) {
            setSessionId(null);
            setMessages([]);
            setIsEnded(false);
        }
    };

    const handleClose = () => {
        router.push("/dialog");
    };

    const handleEndSession = () => {
        if (sessionId && !isEnded) {
            setIsEnded(true);
            autoCompleteSession(sessionId);
        }
    };

    const [isSessionCompleted, setIsSessionCompleted] = useState(false);
    const [sessionFeedbackData, setSessionFeedbackData] = useState<DialogFeedback | null>(null);

    const handleSessionClick = async (clickedSessionId: string) => {
        setSessionId(clickedSessionId);
        setIsLoading(true);
        setError(null);
        setFeedback(null);
        setIsEnded(false);
        setIsSessionCompleted(false);
        setSessionFeedbackData(null);

        try {
            const session = await apiClient.get<{
                messages: DialogMessage[];
                status: string;
                feedback: DialogFeedback | null;
                xpEarned: number;
            }>(`/dialog/sessions/${clickedSessionId}`);

            setMessages(session.messages);

            if (session.status === "completed") {
                setIsSessionCompleted(true);
                setSessionFeedbackData(session.feedback ? { ...session.feedback, xpEarned: session.xpEarned } : null);
                setIsEnded(true);
            } else if (session.messages.some((message) => message.isStopSignal)) {
                setIsEnded(true);
            }
        } catch (loadError) {
            setError(loadError instanceof Error ? loadError.message : "Ошибка загрузки сессии");
        } finally {
            setIsLoading(false);
        }
    };

    const handleShowFeedback = () => {
        if (sessionFeedbackData) {
            setFeedback(sessionFeedbackData);
        }
    };

    const handleNewChat = () => {
        setIsSessionCompleted(false);
        setSessionFeedbackData(null);
        initializeNewSession();
    };

    const handleDeleteSession = async (deleteSessionId: string) => {
        try {
            await deleteDialogSession(deleteSessionId);
            refetchSessions();

            if (sessionId === deleteSessionId) {
                setSessionId(null);
                setMessages([]);
                setFeedback(null);
                setIsEnded(false);
                setIsSessionCompleted(false);
                setSessionFeedbackData(null);
            }
        } catch (deleteError) {
            setError(deleteError instanceof Error ? deleteError.message : "Ошибка удаления");
        }
    };

    const handleVoiceTranscript = useCallback((transcript: string) => {
        const userMessage: DialogMessage = {
            role: "user",
            content: transcript,
            timestamp: new Date().toISOString(),
            isStopSignal: false,
        };
        setMessages((prev) => [...prev, userMessage]);
    }, []);

    const handleVoiceAiResponse = useCallback((content: string, isStopSignal: boolean) => {
        const aiMessage: DialogMessage = {
            role: "assistant",
            content,
            timestamp: new Date().toISOString(),
            isStopSignal,
        };
        setMessages((prev) => [...prev, aiMessage]);

        if (isStopSignal && sessionId) {
            setIsEnded(true);
            autoCompleteSession(sessionId);
        }
    }, [sessionId]);

    const handleVoiceError = useCallback((error: Error) => {
        setVoiceError(error.message);
        setTimeout(() => setVoiceError(null), TimingConstants.fiveSecondsMs);
    }, []);

    const handleVoiceSessionCreated = useCallback((newSessionId: string) => {
        setSessionId(newSessionId);
        setMessages([]);
        refetchSessions();
    }, [refetchSessions]);

    const {
        state: voiceState,
        currentTranscript,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    } = useVoice({
        sessionId,
        modeVoiceEnabled: chatMode === "voice" && (currentMode?.voiceEnabled ?? false),
        bundleId,
        modeId,
        onSessionCreated: handleVoiceSessionCreated,
        onTranscript: handleVoiceTranscript,
        onAiResponse: handleVoiceAiResponse,
        onError: handleVoiceError,
    });

    const isVoiceMode = chatMode === "voice" && currentMode?.voiceEnabled && isVoiceAvailable;

    if (isLoading && !sessionId && !isInitialized) {
        return (
            <div className="chat-screen" style={showSidebar ? undefined : { gridTemplateColumns: "1fr" }}>
                {showSidebar && (
                    <SessionHistorySidebar
                        sessions={filteredSessions}
                        currentSessionId={null}
                        onSessionClick={handleSessionClick}
                        onNewChat={handleNewChat}
                        onDeleteSession={handleDeleteSession}
                        onClose={handleClose}
                    />
                )}
                <main className="dc-main">
                    <div className="dc-head">
                        <span className="dc-head-title">Загрузка...</span>
                    </div>
                    <div className="row center grow">
                        <div style={{ width: 36, height: 36, borderRadius: "50%", border: "3px solid var(--primary)", borderTopColor: "transparent", animation: "spin 0.8s linear infinite" }} />
                    </div>
                </main>
            </div>
        );
    }

    if (error && !sessionId) {
        return (
            <div className="chat-screen" style={showSidebar ? undefined : { gridTemplateColumns: "1fr" }}>
                {showSidebar && (
                    <SessionHistorySidebar
                        sessions={filteredSessions}
                        currentSessionId={null}
                        onSessionClick={handleSessionClick}
                        onNewChat={handleNewChat}
                        onDeleteSession={handleDeleteSession}
                        onClose={handleClose}
                    />
                )}
                <main className="dc-main">
                    <div className="dc-head">
                        <span className="dc-head-title">Ошибка</span>
                    </div>
                    <div className="col center grow" style={{ padding: 16 }}>
                        <div className="empty" style={{ padding: "20px 0 0" }}>
                            <div className="ic" style={{ background: "var(--heart-soft)", color: "var(--heart)" }}>
                                <Icon name="warning" size="xl" />
                            </div>
                            <p className="body" style={{ color: "var(--heart)", fontWeight: 600, marginBottom: 20 }}>{error}</p>
                            <button className="btn btn-outline" onClick={handleClose}>
                                Назад
                            </button>
                        </div>
                    </div>
                </main>
            </div>
        );
    }

    return (
        <div className="chat-screen" style={showSidebar ? undefined : { gridTemplateColumns: "1fr" }}>
            {showSidebar && (
                <SessionHistorySidebar
                    sessions={filteredSessions}
                    currentSessionId={sessionId}
                    onSessionClick={handleSessionClick}
                    onNewChat={handleNewChat}
                    onDeleteSession={handleDeleteSession}
                    onClose={handleClose}
                />
            )}

            <main className="dc-main">
                {/* Header */}
                <div className="dc-head">
                    <button
                        className="icon-btn"
                        onClick={() => setShowSidebar(!showSidebar)}
                        aria-label="История диалогов"
                        style={{ flex: "none" }}
                    >
                        <Icon name="grid" size="md" />
                    </button>

                    <span className="itile primary" style={{ width: 36, height: 36, borderRadius: "50%", flex: "none" }}>
                        <Icon name="sparkle" size={18} />
                    </span>
                    <div style={{ minWidth: 0, flex: 1 }}>
                        <div className="dc-head-title">
                            {currentMode?.title || "ИИ-собеседник"}
                        </div>
                        <div className="dc-head-sub">
                            {currentBundle?.title || "Практика диалогов"} · текстовый режим
                        </div>
                    </div>

                    {/* Timer */}
                    {sessionId && (
                        <span className="chip num" style={{ flex: "none", fontFamily: "var(--font-mono)", fontSize: 12 }}>
                            <Icon name="clock" size="sm" />
                            {formatTime(sessionTimer)}
                        </span>
                    )}

                    {/* End session button */}
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

                    {/* Close button */}
                    <button className="icon-btn" onClick={handleClose} aria-label="Закрыть" style={{ flex: "none" }}>
                        <Icon name="close" size="md" />
                    </button>
                </div>

                {/* Status bar */}
                {(isSending || isCompleting || voiceState !== "idle") && (
                    <div className="dc-status-bar" role="status" aria-live="polite">
                        {isCompleting && (
                            <>
                                <span style={{ width: 14, height: 14, border: "2px solid var(--primary)", borderTopColor: "transparent", borderRadius: "50%", animation: "spin 0.8s linear infinite", display: "inline-block", flex: "none" }} />
                                Формирую обратную связь...
                            </>
                        )}
                        {isSending && !isCompleting && (
                            <>
                                <span style={{ width: 14, height: 14, border: "2px solid var(--primary)", borderTopColor: "transparent", borderRadius: "50%", animation: "spin 0.8s linear infinite", display: "inline-block", flex: "none" }} />
                                ИИ думает...
                            </>
                        )}
                        {!isSending && !isCompleting && voiceState === "listening" && "Слушаю..."}
                        {!isSending && !isCompleting && voiceState === "speaking" && "Говори..."}
                        {!isSending && !isCompleting && voiceState === "processing" && "Обрабатываю речь..."}
                        {!isSending && !isCompleting && voiceState === "playing" && "ИИ отвечает..."}
                    </div>
                )}

                {/* Messages area */}
                <div className="dc-thread">
                    <div className="dc-thread-inner">
                        {!sessionId && messages.length === 0 && (
                            <div className="empty" style={{ padding: "80px 20px" }}>
                                <div className="ic" style={{ background: "var(--primary-soft)", color: "var(--primary)" }}>
                                    <Icon name="phone" size="xl" />
                                </div>
                                <p className="h4" style={{ marginBottom: 8 }}>Начни разговор</p>
                                <p className="small" style={{ maxWidth: 320, margin: "0 auto" }}>
                                    Представься и произнеси вступление — ты звонишь клиенту первым
                                </p>
                            </div>
                        )}

                        {messages.map((message, messageIndex) => (
                            <ChatMessage key={messageIndex} message={message} />
                        ))}

                        {isSending && (
                            <div className="dc-msg ai">
                                <span className="dc-avatar" aria-hidden="true">
                                    <Icon name="sparkle" size="sm" />
                                </span>
                                <div className="dc-bubble-wrap">
                                    <div className="dc-bubble typing">
                                        <span />
                                        <span />
                                        <span />
                                    </div>
                                </div>
                            </div>
                        )}

                        {error && sessionId && (
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

                {/* Input area */}
                <div className="dc-input">
                    {voiceError && (
                        <div className="row center gap-2 small" style={{ color: "var(--heart)" }}>
                            <Icon name="warning" size="sm" />
                            {voiceError}
                        </div>
                    )}

                    {currentTranscript && (
                        <div className="dc-interim">
                            «{currentTranscript}»
                        </div>
                    )}

                    {isSessionCompleted && sessionFeedbackData && !feedback && (
                        <button className="btn btn-dark btn-block" onClick={handleShowFeedback}>
                            <Icon name="book" size="sm" />
                            Показать обратную связь
                        </button>
                    )}

                    {/* Voice mode controls */}
                    {isVoiceMode && !isEnded && !feedback ? (
                        <div className="col center gap-4">
                            <VoiceMicButton
                                state={voiceState}
                                isAvailable={isVoiceAvailable}
                                onStart={startVoice}
                                onStop={stopVoice}
                            />
                        </div>
                    ) : (
                        /* Text mode input */
                        <ChatInput
                            onSend={handleSendMessage}
                            disabled={isSending || isCompleting || isEnded || !!feedback}
                            placeholder={isEnded || feedback ? "Диалог завершён" : "Напиши сообщение…"}
                        />
                    )}
                </div>
            </main>

            {feedback && (
                <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />
            )}
        </div>
    );
}
