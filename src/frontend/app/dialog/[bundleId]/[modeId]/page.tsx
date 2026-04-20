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
} from "@/lib/hooks/useDialog";
import { useVoice } from "@/lib/hooks/useVoice";
import { apiClient } from "@/lib/api/apiClient";
import { ChatMessage } from "@/components/dialog/ChatMessage";
import { ChatInput } from "@/components/dialog/ChatInput";
import { FeedbackModal } from "@/components/dialog/FeedbackModal";
import { SessionHistorySidebar } from "@/components/dialog/SessionHistorySidebar";
import { VoiceMicButton } from "@/components/dialog/VoiceMicButton";
import { Icon } from "@/components/ui/Icon";

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
    const chatMode = searchParams.get("mode") || "text"; // "text" or "voice"

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

    // Voice handling
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
        setTimeout(() => setVoiceError(null), 5000);
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

    // Loading state
    if (isLoading && !sessionId && !isInitialized) {
        return (
            <div className="flex h-screen bg-surface">
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
                <div className="flex-1 flex flex-col">
                    <header className="flex items-center gap-4 px-4 py-3 border-b border-outline-variant bg-surface-container-lowest">
                        <button
                            onClick={() => setShowSidebar(!showSidebar)}
                            className="text-on-surface-variant hover:text-on-surface tonal-transition md:hidden"
                        >
                            <Icon name="grid" size="md" />
                        </button>
                        <h1 className="font-headline font-bold text-on-surface">Загрузка...</h1>
                    </header>
                    <div className="flex-1 flex items-center justify-center">
                        <div className="w-10 h-10 rounded-full border-4 border-primary border-t-transparent animate-spin" />
                    </div>
                </div>
            </div>
        );
    }

    // Error state
    if (error && !sessionId) {
        return (
            <div className="flex h-screen bg-surface">
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
                <div className="flex-1 flex flex-col">
                    <header className="flex items-center gap-4 px-4 py-3 border-b border-outline-variant bg-surface-container-lowest">
                        <h1 className="font-headline font-bold text-on-surface">Ошибка</h1>
                    </header>
                    <div className="flex-1 flex flex-col items-center justify-center p-4">
                        <div className="w-16 h-16 rounded-full bg-error-container flex items-center justify-center mb-4">
                            <Icon name="warning" size="xl" className="text-error" />
                        </div>
                        <p className="text-error text-center mb-6 font-medium">{error}</p>
                        <button
                            onClick={handleClose}
                            className="px-6 py-3 bg-surface-container text-on-surface font-semibold rounded-full hover:bg-surface-container-high tonal-transition"
                        >
                            Вернуться
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="flex h-screen bg-surface">
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

            <div className="flex-1 flex flex-col">
                {/* Header */}
                <header className="flex items-center gap-4 px-4 py-3 border-b border-outline-variant bg-surface-container-lowest flex-shrink-0">
                    <button
                        onClick={() => setShowSidebar(!showSidebar)}
                        className="text-on-surface-variant hover:text-on-surface tonal-transition"
                    >
                        <Icon name="grid" size="md" />
                    </button>

                    {/* Persona card */}
                    <div className="flex items-center gap-3 flex-1 min-w-0">
                        <div className="w-10 h-10 rounded-full bg-secondary-container flex items-center justify-center shrink-0">
                            <Icon name="sparkle" size="md" className="text-secondary" />
                        </div>
                        <div className="min-w-0">
                            <h1 className="font-semibold text-on-surface truncate text-sm">
                                {currentMode?.title || "AI Собеседник"}
                            </h1>
                            <p className="text-xs text-on-surface-variant truncate">
                                {currentBundle?.title || "Тренировка диалога"}
                            </p>
                        </div>
                    </div>

                    {/* Timer */}
                    {sessionId && (
                        <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-surface-container text-on-surface-variant">
                            <Icon name="clock" size="sm" />
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
                            <Icon name="close" size="sm" />
                            Завершить
                        </button>
                    )}

                    {/* Close button */}
                    <button
                        onClick={handleClose}
                        className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                    >
                        <Icon name="close" size="md" className="text-on-surface-variant" />
                    </button>
                </header>

                {/* Status bar */}
                {(isSending || isCompleting || voiceState !== "idle") && (
                    <div className="px-4 py-2 bg-surface-container-low border-b border-outline-variant">
                        <p className="text-sm text-on-surface-variant text-center flex items-center justify-center gap-2">
                            {isCompleting && (
                                <>
                                    <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                                    Формируем обратную связь...
                                </>
                            )}
                            {isSending && !isCompleting && (
                                <>
                                    <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                                    AI думает...
                                </>
                            )}
                            {voiceState === "listening" && "🎙️ Слушаю..."}
                            {voiceState === "speaking" && "🗣️ Говорите..."}
                            {voiceState === "processing" && "⏳ Обработка речи..."}
                            {voiceState === "playing" && "🔊 AI отвечает..."}
                        </p>
                    </div>
                )}

                {/* Messages area */}
                <div className="flex-1 overflow-y-auto p-4 space-y-4">
                    {!sessionId && messages.length === 0 && (
                        <div className="flex-1 flex items-center justify-center h-full">
                            <div className="text-center max-w-sm">
                                <div className="w-16 h-16 rounded-full bg-primary-container flex items-center justify-center mx-auto mb-4">
                                    <Icon name="phone" size="xl" className="text-primary" />
                                </div>
                                <p className="font-semibold text-on-surface mb-2">Начните разговор</p>
                                <p className="text-sm text-on-surface-variant">
                                    Представьтесь и скажите свой опеннер — вы звоните клиенту первым
                                </p>
                            </div>
                        </div>
                    )}

                    {messages.map((message, messageIndex) => (
                        <ChatMessage key={messageIndex} message={message} />
                    ))}

                    {isSending && (
                        <div className="flex justify-start">
                            <div className="w-8 h-8 rounded-full bg-secondary-container flex items-center justify-center mr-2">
                                <Icon name="sparkle" size="sm" className="text-secondary" />
                            </div>
                            <div className="bg-surface-container px-4 py-3 rounded-2xl rounded-tl-sm">
                                <div className="flex gap-1">
                                    <span className="w-2 h-2 bg-on-surface-variant rounded-full animate-bounce" />
                                    <span className="w-2 h-2 bg-on-surface-variant rounded-full animate-bounce [animation-delay:0.1s]" />
                                    <span className="w-2 h-2 bg-on-surface-variant rounded-full animate-bounce [animation-delay:0.2s]" />
                                </div>
                            </div>
                        </div>
                    )}

                    {error && sessionId && (
                        <div className="flex justify-center">
                            <div className="bg-error-container text-error text-sm px-4 py-2 rounded-full flex items-center gap-2">
                                <Icon name="warning" size="sm" />
                                {error}
                            </div>
                        </div>
                    )}

                    <div ref={messagesEndRef} />
                </div>

                {/* Input area */}
                <div className="flex-shrink-0 p-4 border-t border-outline-variant bg-surface-container-lowest pb-[env(safe-area-inset-bottom)]">
                    {voiceError && (
                        <div className="text-center text-error text-sm mb-3 flex items-center justify-center gap-2">
                            <Icon name="warning" size="sm" />
                            {voiceError}
                        </div>
                    )}

                    {currentTranscript && (
                        <div className="text-center text-on-surface-variant text-sm mb-3 italic bg-surface-container rounded-full px-4 py-2">
                            "{currentTranscript}"
                        </div>
                    )}

                    {isSessionCompleted && sessionFeedbackData && !feedback && (
                        <button
                            onClick={handleShowFeedback}
                            className="w-full py-3 mb-3 bg-primary text-on-primary font-bold rounded-full shadow-[0_4px_0_var(--color-primary-dim)] active:shadow-none active:translate-y-1 tonal-transition flex items-center justify-center gap-2"
                        >
                            <Icon name="book" size="sm" />
                            Показать обратную связь
                        </button>
                    )}

                    {/* Voice mode controls */}
                    {isVoiceMode && !isEnded && !feedback ? (
                        <div className="flex flex-col items-center gap-4">
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
                            placeholder={isEnded || feedback ? "Диалог завершён" : "Введите сообщение..."}
                        />
                    )}
                </div>
            </div>

            {feedback && (
                <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />
            )}
        </div>
    );
}
