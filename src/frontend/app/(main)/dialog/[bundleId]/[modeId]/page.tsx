"use client";

import { useParams, useRouter } from "next/navigation";
import { useState, useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
    useDialogBundles,
    useDialogModes,
    useDialogSessions,
    useDialogSession,
    DialogMessage,
    DialogFeedback,
    startDialogSession,
    sendDialogMessage,
    completeDialogSession,
} from "@/lib/hooks/useDialog";
import { ChatMessage } from "@/components/dialog/ChatMessage";
import { ChatInput } from "@/components/dialog/ChatInput";
import { FeedbackModal } from "@/components/dialog/FeedbackModal";
import { SessionHistorySidebar } from "@/components/dialog/SessionHistorySidebar";

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
    const [showCompletionButton, setShowCompletionButton] = useState(false);
    const [showSidebar, setShowSidebar] = useState(true);

    const messagesEndRef = useRef<HTMLDivElement>(null);

    const scrollToBottom = () => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    };

    useEffect(() => {
        scrollToBottom();
    }, [messages]);

    const initializeNewSession = async () => {
        try {
            setIsLoading(true);
            setError(null);
            setFeedback(null);
            setShowCompletionButton(false);
            const session = await startDialogSession(bundleId, modeId);
            setSessionId(session.id);
            setMessages(session.messages);
            refetchSessions();

            if (session.messages.some((message) => message.isStopSignal)) {
                setShowCompletionButton(true);
            }
        } catch (sessionError) {
            setError(sessionError instanceof Error ? sessionError.message : "Ошибка запуска сессии");
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        initializeNewSession();
    }, [bundleId, modeId]);

    const handleSendMessage = async (content: string) => {
        if (!sessionId || isSending) return;

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
            const aiMessage = await sendDialogMessage(sessionId, content);
            setMessages((previousMessages) => [...previousMessages, aiMessage]);

            if (aiMessage.isStopSignal) {
                setShowCompletionButton(true);
            }
        } catch (sendError) {
            setError(sendError instanceof Error ? sendError.message : "Ошибка отправки");
        } finally {
            setIsSending(false);
        }
    };

    const handleCompleteSession = async () => {
        if (!sessionId || isCompleting) return;

        setIsCompleting(true);
        setError(null);

        try {
            const sessionFeedback = await completeDialogSession(sessionId);
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
        initializeNewSession();
    };

    const handleClose = () => {
        router.push("/dialog");
    };

    const handleSessionClick = async (clickedSessionId: string) => {
        setSessionId(clickedSessionId);
        setIsLoading(true);
        setError(null);
        setFeedback(null);
        setShowCompletionButton(false);

        try {
            const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000"}/dialog/sessions/${clickedSessionId}`, {
                headers: {
                    Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
                },
            });

            if (!response.ok) throw new Error("Failed to load session");

            const session = await response.json();
            setMessages(session.messages);

            if (session.status === "completed") {
                setFeedback(session.feedback);
            } else if (session.messages.some((message: DialogMessage) => message.isStopSignal)) {
                setShowCompletionButton(true);
            }
        } catch (loadError) {
            setError(loadError instanceof Error ? loadError.message : "Ошибка загрузки сессии");
        } finally {
            setIsLoading(false);
        }
    };

    const handleNewChat = () => {
        initializeNewSession();
    };

    const filteredSessions = allSessions?.filter(
        (session) => session.bundleId === bundleId && session.modeId === modeId
    ) ?? [];

    if (isLoading && !sessionId) {
        return (
            <div className="flex h-screen bg-white">
                {showSidebar && (
                    <SessionHistorySidebar
                        sessions={filteredSessions}
                        currentSessionId={null}
                        onSessionClick={handleSessionClick}
                        onNewChat={handleNewChat}
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
                        <button
                            onClick={handleClose}
                            className="text-2xl text-gray-400 hover:text-gray-600"
                        >
                            ✕
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
                    />
                )}
                <div className="flex-1 flex flex-col">
                    <header className="flex items-center gap-4 px-4 py-3 border-b border-gray-100">
                        <button
                            onClick={handleClose}
                            className="text-2xl text-gray-400 hover:text-gray-600"
                        >
                            ✕
                        </button>
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
                    <button
                        onClick={handleClose}
                        className="text-2xl text-gray-400 hover:text-gray-600"
                    >
                        ✕
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
                    {showCompletionButton && !feedback && (
                        <button
                            onClick={handleCompleteSession}
                            disabled={isCompleting}
                            className="w-full mb-3 py-3 bg-blue-500 text-white font-bold rounded-2xl hover:bg-blue-600 disabled:bg-gray-300 disabled:cursor-not-allowed transition-colors"
                        >
                            {isCompleting ? "Формируем обратную связь..." : "Завершить диалог"}
                        </button>
                    )}

                    <ChatInput
                        onSend={handleSendMessage}
                        disabled={isSending || isCompleting || !!feedback}
                        placeholder={feedback ? "Диалог завершён" : "Напишите ответ..."}
                    />
                </div>
            </div>

            {feedback && (
                <FeedbackModal feedback={feedback} onClose={handleCloseFeedback} />
            )}
        </div>
    );
}
