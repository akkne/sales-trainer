"use client";

import { useParams, useRouter } from "next/navigation";
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

export default function ChatPage() {
    const params = useParams();
    const router = useRouter();
    const queryClient = useQueryClient();
    const bundleId = params.bundleId as string;
    const modeId = params.modeId as string;

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

    const messagesEndRef = useRef<HTMLDivElement>(null);

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

    const {
        state: voiceState,
        currentTranscript,
        isVoiceAvailable,
        startVoice,
        stopVoice,
    } = useVoice({
        sessionId,
        modeVoiceEnabled: currentMode?.voiceEnabled ?? false,
        onTranscript: handleVoiceTranscript,
        onAiResponse: handleVoiceAiResponse,
        onError: handleVoiceError,
    });

    if (isLoading && !sessionId && !isInitialized) {
        return (
            <div className="flex h-screen bg-white">
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
                    <header className="flex items-center gap-4 px-4 py-3 border-b border-gray-100">
                        <button
                            onClick={() => setShowSidebar(!showSidebar)}
                            className="text-gray-400 hover:text-gray-600 md:hidden"
                        >
                            ☰
                        </button>
                        <h1 className="font-bold text-gray-800">Загрузка...</h1>
                    </header>
                    <div className="flex-1 flex items-center justify-center">
                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#58CC02]" />
                    </div>
                </div>
            </div>
        );
    }

    if (error && !sessionId) {
        return (
            <div className="flex h-screen bg-white">
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
                    <header className="flex items-center gap-4 px-4 py-3 border-b border-gray-100">
                        <h1 className="font-bold text-gray-800">Ошибка</h1>
                    </header>
                    <div className="flex-1 flex flex-col items-center justify-center p-4">
                        <p className="text-red-500 text-center mb-4">{error}</p>
                        <button
                            onClick={handleClose}
                            className="px-6 py-3 bg-gray-200 text-gray-700 font-bold rounded-2xl hover:bg-gray-300"
                        >
                            Вернуться
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="flex h-screen bg-white">
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
                <header className="flex items-center gap-4 px-4 py-3 border-b border-gray-100 flex-shrink-0">
                    <button
                        onClick={() => setShowSidebar(!showSidebar)}
                        className="text-gray-400 hover:text-gray-600"
                    >
                        ☰
                    </button>
                    <div className="flex-1 min-w-0">
                        <h1 className="font-bold text-gray-800 truncate">
                            {currentMode?.title || "Диалог"}
                        </h1>
                        {currentBundle && (
                            <p className="text-xs text-gray-500">{currentBundle.title}</p>
                        )}
                    </div>
                </header>

                <div className="flex-1 overflow-y-auto p-4 space-y-4">
                    {!sessionId && messages.length === 0 && (
                        <div className="flex-1 flex items-center justify-center h-full">
                            <div className="text-center text-gray-400">
                                <p className="mb-2">Представьтесь и скажите свой опеннер</p>
                                <p className="text-sm">Вы звоните клиенту — начните разговор первым</p>
                            </div>
                        </div>
                    )}

                    {messages.map((message, messageIndex) => (
                        <ChatMessage key={messageIndex} message={message} />
                    ))}

                    {isSending && (
                        <div className="flex justify-start">
                            <div className="bg-gray-100 px-4 py-3 rounded-2xl rounded-tl-sm">
                                <div className="flex gap-1">
                                    <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" />
                                    <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:0.1s]" />
                                    <span className="w-2 h-2 bg-gray-400 rounded-full animate-bounce [animation-delay:0.2s]" />
                                </div>
                            </div>
                        </div>
                    )}

                    {error && sessionId && (
                        <div className="text-center text-red-500 text-sm py-2">
                            {error}
                        </div>
                    )}

                    <div ref={messagesEndRef} />
                </div>

                <div className="flex-shrink-0 p-4 border-t border-gray-100 pb-[env(safe-area-inset-bottom)]">
                    {isCompleting && (
                        <div className="text-center text-sm text-gray-500 mb-3">
                            Формируем обратную связь...
                        </div>
                    )}

                    {voiceError && (
                        <div className="text-center text-red-500 text-sm mb-3">
                            {voiceError}
                        </div>
                    )}

                    {currentTranscript && (
                        <div className="text-center text-gray-500 text-sm mb-3 italic">
                            {currentTranscript}
                        </div>
                    )}

                    {isSessionCompleted && sessionFeedbackData && !feedback && (
                        <button
                            onClick={handleShowFeedback}
                            className="w-full py-3 mb-3 bg-[#58CC02] text-white font-bold rounded-2xl hover:bg-[#4CAD02] transition-colors"
                        >
                            Показать обратную связь
                        </button>
                    )}

                    <div className="flex items-center gap-4">
                        {/* Voice-only mode: show only mic button */}
                        {currentMode?.voiceEnabled && isVoiceAvailable ? (
                            <div className="flex-1 flex justify-center">
                                <VoiceMicButton
                                    state={voiceState}
                                    isAvailable={isVoiceAvailable}
                                    onStart={startVoice}
                                    onStop={stopVoice}
                                />
                            </div>
                        ) : (
                            /* Text-only mode: show only text input */
                            <div className="flex-1">
                                <ChatInput
                                    onSend={handleSendMessage}
                                    disabled={isSending || isCompleting || isEnded || !!feedback}
                                    placeholder={isEnded || feedback ? "Диалог завершён" : "Ваш опеннер..."}
                                />
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {feedback && (
                <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />
            )}
        </div>
    );
}
