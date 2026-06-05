"use client";

import { useState, useRef, useEffect } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { apiClient } from "@/shared/api/api-client";

interface ChatMessage {
    role: "user" | "assistant";
    content: string;
}

interface AiDialogueContent {
    persona: string;
    scenario: string;
    context?: string;
    max_turns?: number;
    success_criteria?: string[];
    ai_prompt?: string;
}

interface AiDialogueExerciseProps {
    content: AiDialogueContent;
    exerciseId: string;
    onSubmit: (answer: { messages: ChatMessage[]; completedNaturally: boolean }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function AiDialogueExercise({
    content,
    exerciseId,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: AiDialogueExerciseProps) {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [inputText, setInputText] = useState("");
    const [isSending, setIsSending] = useState(false);
    const [isComplete, setIsComplete] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const maxTurns = content.max_turns ?? 6;
    const minTurns = Math.floor(maxTurns / 2);
    const userTurnCount = messages.filter(m => m.role === "user").length;
    const canComplete = userTurnCount >= minTurns;

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);

    // Start conversation with AI greeting on mount
    useEffect(() => {
        if (messages.length === 0 && !isAnswered) {
            startConversation();
        }
    }, []);

    async function startConversation() {
        setIsSending(true);
        try {
            const data = await apiClient.post<{ response: string; isComplete: boolean; isFinished: boolean }>(
                `/exercises/${exerciseId}/chat`,
                { message: "" }
            );
            setMessages([{ role: "assistant", content: data.response }]);
            if (data.isComplete || data.isFinished) setIsComplete(true);
        } catch (error) {
            console.error("Failed to start conversation:", error);
            // Fallback greeting
            setMessages([{
                role: "assistant",
                content: `${content.persona}: Да, слушаю. Что вы хотели?`
            }]);
        } finally {
            setIsSending(false);
        }
    }

    async function sendMessage() {
        if (!inputText.trim() || isSending || isComplete || isAnswered) return;

        const userMessage: ChatMessage = { role: "user", content: inputText };
        setMessages([...messages, userMessage]);
        setInputText("");
        setIsSending(true);

        try {
            const data = await apiClient.post<{ response: string; isComplete: boolean; isFinished: boolean }>(
                `/exercises/${exerciseId}/chat`,
                { message: inputText }
            );
            setMessages(prev => [...prev, { role: "assistant", content: data.response }]);
            if (data.isComplete || data.isFinished) setIsComplete(true);
        } catch (error) {
            console.error("Failed to send message:", error);
            setMessages(prev => [...prev, {
                role: "assistant",
                content: "Понял вас. Что ещё хотели обсудить?"
            }]);
        } finally {
            setIsSending(false);
        }
    }

    function handleComplete() {
        onSubmit({
            messages,
            completedNaturally: isComplete,
        });
    }

    function handleKeyDown(e: React.KeyboardEvent) {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    }

    function getRatingColor(score: number): string {
        if (score >= 80) return "bg-primary text-on-primary";
        if (score >= 60) return "bg-tertiary text-on-tertiary";
        return "bg-error text-on-error";
    }

    return (
        <div className="flex flex-col gap-4 h-full">
            {/* Persona header */}
            <div className="flex items-center gap-3 p-3 bg-surface-container rounded-xl">
                <div className="w-12 h-12 rounded-full bg-primary-container flex items-center justify-center">
                    <Icon name="user" size="md" className="text-primary" />
                </div>
                <div>
                    <p className="font-bold text-on-surface">{content.persona}</p>
                    <p className="text-sm text-on-surface-variant">{content.scenario}</p>
                </div>
            </div>

            {content.context && (
                <p className="text-sm text-on-surface-variant px-1">{content.context}</p>
            )}

            {/* Chat messages */}
            <div className="flex-1 overflow-y-auto flex flex-col gap-3 min-h-[200px] max-h-[400px] p-2">
                {messages.map((msg, idx) => (
                    <div
                        key={idx}
                        className={`flex ${msg.role === "user" ? "justify-end" : "justify-start"}`}
                    >
                        <div
                            className={`max-w-[80%] px-4 py-2 rounded-2xl ${
                                msg.role === "user"
                                    ? "bg-primary text-on-primary rounded-br-sm"
                                    : "bg-surface-container text-on-surface rounded-bl-sm"
                            }`}
                        >
                            {msg.content}
                        </div>
                    </div>
                ))}
                {isSending && (
                    <div className="flex justify-start">
                        <div className="px-4 py-2 rounded-2xl bg-surface-container text-on-surface-variant rounded-bl-sm">
                            <span className="animate-pulse">Печатает...</span>
                        </div>
                    </div>
                )}
                <div ref={messagesEndRef} />
            </div>

            {/* Input or result */}
            {!isAnswered && !isComplete && (
                <div className="flex gap-2">
                    <input
                        type="text"
                        value={inputText}
                        onChange={(e) => setInputText(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder="Ваша реплика..."
                        disabled={isSending}
                        className="flex-1 px-4 py-3 rounded-full bg-surface-container border-2 border-outline-variant focus:border-primary outline-none"
                    />
                    <button
                        onClick={sendMessage}
                        disabled={!inputText.trim() || isSending}
                        className="w-12 h-12 rounded-full bg-primary text-on-primary flex items-center justify-center disabled:opacity-40"
                    >
                        <Icon name="send" size="sm" />
                    </button>
                </div>
            )}

            {isAnswered && (
                <div className="flex flex-col gap-3 p-4 bg-surface-container rounded-xl">
                    <div className="flex items-center gap-3">
                        <span className={`px-3 py-1 rounded-full text-sm font-bold ${getRatingColor(submittedResult.score)}`}>
                            {Math.round(submittedResult.score / 10)}/10
                        </span>
                        <span className={`font-medium ${submittedResult.isCorrect ? "text-primary" : "text-error"}`}>
                            {submittedResult.isCorrect ? "Отличный диалог!" : "Есть что улучшить"}
                        </span>
                    </div>
                    {submittedResult.aiFeedback && (
                        <p className="text-sm text-on-surface-variant leading-relaxed">
                            {submittedResult.aiFeedback}
                        </p>
                    )}
                </div>
            )}

            {/* Action buttons */}
            <div className="flex gap-3">
                {!isAnswered && onSkip && (
                    <button
                        onClick={onSkip}
                        disabled={isSubmitting}
                        className="flex-1 py-4 rounded-full border-2 border-outline-variant text-on-surface-variant font-extrabold hover:border-outline hover:text-on-surface transition-colors disabled:opacity-40"
                    >
                        Пропустить
                    </button>
                )}

                {isAnswered ? (
                    <button
                        onClick={onContinue}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d"
                    >
                        Продолжить
                    </button>
                ) : (canComplete || isComplete) && (
                    <button
                        onClick={handleComplete}
                        disabled={isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Анализируем..." : "Завершить диалог"}
                    </button>
                )}
            </div>

            {!isAnswered && !canComplete && (
                <p className="text-xs text-on-surface-variant text-center">
                    Ещё {minTurns - userTurnCount} реплик до возможности завершить
                </p>
            )}
        </div>
    );
}
