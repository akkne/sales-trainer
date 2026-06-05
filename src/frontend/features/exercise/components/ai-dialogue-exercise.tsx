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

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, height: "100%" }}>
            {/* Persona header */}
            <div
                style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    padding: 12,
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                }}
            >
                <GeoAvatar seed={content.persona} size={48} />
                <div>
                    <p style={{ fontWeight: 600, margin: 0 }}>{content.persona}</p>
                    <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0 }}>{content.scenario}</p>
                </div>
            </div>

            {content.context && (
                <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0 }}>{content.context}</p>
            )}

            {/* Chat messages */}
            <div
                style={{
                    flex: 1,
                    overflowY: "auto",
                    display: "flex",
                    flexDirection: "column",
                    gap: 12,
                    minHeight: 200,
                    maxHeight: 400,
                    padding: 8,
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                }}
            >
                {messages.map((msg, idx) => (
                    <div
                        key={idx}
                        style={{
                            display: "flex",
                            justifyContent: msg.role === "user" ? "flex-end" : "flex-start",
                        }}
                    >
                        <div
                            style={{
                                maxWidth: "80%",
                                padding: "10px 14px",
                                borderRadius: msg.role === "user" ? "14px 14px 4px 14px" : "4px 14px 14px 14px",
                                background: msg.role === "user" ? "var(--indigo)" : "var(--bg-2)",
                                color: msg.role === "user" ? "white" : "var(--ink)",
                                fontSize: 14,
                                lineHeight: 1.4,
                            }}
                        >
                            {msg.content}
                        </div>
                    </div>
                ))}
                {isSending && (
                    <div style={{ display: "flex", justifyContent: "flex-start" }}>
                        <div
                            style={{
                                padding: "10px 14px",
                                borderRadius: "4px 14px 14px 14px",
                                background: "var(--bg-2)",
                                color: "var(--ink-3)",
                                fontSize: 14,
                            }}
                        >
                            <span style={{ opacity: 0.7 }}>Печатает...</span>
                        </div>
                    </div>
                )}
                <div ref={messagesEndRef} />
            </div>

            {/* Input or result */}
            {!isAnswered && !isComplete && (
                <div style={{ display: "flex", gap: 8 }}>
                    <input
                        type="text"
                        value={inputText}
                        onChange={(e) => setInputText(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder="Ваша реплика…"
                        disabled={isSending}
                        style={{
                            flex: 1,
                            padding: "10px 14px",
                            border: "1px solid var(--line-2)",
                            borderRadius: 10,
                            fontFamily: "var(--f-sans)",
                            fontSize: 14,
                            outline: "none",
                            background: "var(--surface)",
                        }}
                    />
                    <Button
                        variant="accent"
                        onClick={sendMessage}
                        disabled={!inputText.trim() || isSending}
                    >
                        <Icon name="send" size="sm" />
                    </Button>
                </div>
            )}

            {!isAnswered && !canComplete && (
                <p style={{ fontSize: 12, color: "var(--ink-4)", textAlign: "center", margin: 0 }}>
                    Ещё {minTurns - userTurnCount} реплик до возможности завершить
                </p>
            )}

            {/* Footer */}
            {isAnswered ? (
                <ExerciseResultBanner
                    isCorrect={submittedResult.isCorrect}
                    score={submittedResult.score}
                    explanation={submittedResult.explanation ?? null}
                    aiFeedback={submittedResult.aiFeedback ?? null}
                    xpEarned={submittedResult.xpEarned}
                    onContinue={onContinue ?? (() => {})}
                />
            ) : (canComplete || isComplete) && (
                <div
                    style={{
                        position: "fixed",
                        bottom: 0,
                        left: 0,
                        right: 0,
                        background: "var(--surface)",
                        borderTop: "1px solid var(--line)",
                        padding: "20px 32px",
                        paddingBottom: "max(20px, env(safe-area-inset-bottom))",
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            maxWidth: 820,
                            margin: "0 auto",
                        }}
                    >
                        {onSkip && (
                            <Button variant="ghost" onClick={onSkip} disabled={isSubmitting}>
                                ПРОПУСТИТЬ
                            </Button>
                        )}
                        <div style={{ display: "flex", alignItems: "center", gap: 16, marginLeft: "auto" }}>
                            <Button
                                variant="accent"
                                size="lg"
                                onClick={handleComplete}
                                disabled={isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ЗАВЕРШИТЬ ДИАЛОГ
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
