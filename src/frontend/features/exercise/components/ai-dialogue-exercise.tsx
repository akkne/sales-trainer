"use client";

import { useState, useRef, useEffect, useCallback } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { apiClient } from "@/shared/api/api-client";
import { useExerciseVoice } from "@/features/exercise/hooks/use-exercise-voice";
import { VoiceMicButton } from "@/features/voice/components/voice-mic-button";

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

type DialogueMode = "text" | "voice";

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
    const [mode, setMode] = useState<DialogueMode | null>(null);
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [inputText, setInputText] = useState("");
    const [isSending, setIsSending] = useState(false);
    const [isComplete, setIsComplete] = useState(false);
    const [voiceError, setVoiceError] = useState<string | null>(null);
    const messagesEndRef = useRef<HTMLDivElement>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const maxTurns = content.max_turns ?? 6;
    const minTurns = Math.floor(maxTurns / 2);
    const userTurnCount = messages.filter(m => m.role === "user").length;
    const canComplete = userTurnCount >= minTurns;

    // --- Voice pipeline (same services as live calls) ---
    const handleVoiceTranscript = useCallback((transcript: string) => {
        setMessages(prev => [...prev, { role: "user", content: transcript }]);
    }, []);

    const handleVoiceResponse = useCallback((aiContent: string, isStopSignal: boolean) => {
        if (aiContent.trim()) {
            setMessages(prev => [...prev, { role: "assistant", content: aiContent }]);
        }
        if (isStopSignal) setIsComplete(true);
    }, []);

    const handleVoiceError = useCallback((error: Error) => {
        setVoiceError(error.message);
    }, []);

    const voice = useExerciseVoice({
        exerciseId,
        onTranscript: handleVoiceTranscript,
        onAiResponse: handleVoiceResponse,
        onError: handleVoiceError,
    });

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);

    useEffect(() => {
        // Stop the voice pipeline once the dialog is complete or submitted.
        if ((isComplete || isAnswered) && voice.state !== "idle") {
            voice.stopVoice();
        }
    }, [isComplete, isAnswered, voice]);

    async function sendMessage() {
        if (!inputText.trim() || isSending || isComplete || isAnswered) return;

        const messageText = inputText;
        setMessages(prev => [...prev, { role: "user", content: messageText }]);
        setInputText("");
        setIsSending(true);

        try {
            const data = await apiClient.post<{ response: string; isComplete: boolean; isFinished: boolean }>(
                `/exercises/${exerciseId}/chat`,
                { message: messageText }
            );
            if (data.response.trim()) {
                setMessages(prev => [...prev, { role: "assistant", content: data.response }]);
            }
            if (data.isComplete || data.isFinished) setIsComplete(true);
        } catch (error) {
            console.error("Failed to send message:", error);
            setMessages(prev => [...prev, {
                role: "assistant",
                content: "Got it. What else did you want to discuss?"
            }]);
        } finally {
            setIsSending(false);
        }
    }

    function handleComplete() {
        if (voice.state !== "idle") voice.stopVoice();
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

    const showInput = !isAnswered && !isComplete && mode === "text";
    const showVoiceControls = !isAnswered && !isComplete && mode === "voice";

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, height: "100%" }}>
            {/* Exercise type chip */}
            <div><span className="ex-chip ex-chip--dialogue">AI dialogue</span></div>

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

            {/* Mode selection — shown before the dialog starts */}
            {mode === null && !isAnswered && (
                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                    <p style={{ fontSize: 14, color: "var(--ink-2)", margin: 0 }}>
                        You call first. Choose how to run the dialogue:
                    </p>
                    <div style={{ display: "flex", gap: 12 }}>
                        <button
                            onClick={() => setMode("text")}
                            style={modeChoiceStyle}
                        >
                            <Icon name="send" size="lg" />
                            <span style={{ fontWeight: 600 }}>Text</span>
                            <span style={{ fontSize: 12, color: "var(--ink-3)" }}>Type your lines</span>
                        </button>
                        <button
                            onClick={() => { setVoiceError(null); setMode("voice"); }}
                            disabled={!voice.isVoiceAvailable}
                            style={{
                                ...modeChoiceStyle,
                                opacity: voice.isVoiceAvailable ? 1 : 0.5,
                                cursor: voice.isVoiceAvailable ? "pointer" : "not-allowed",
                            }}
                        >
                            <Icon name="mic" size="lg" />
                            <span style={{ fontWeight: 600 }}>Voice</span>
                            <span style={{ fontSize: 12, color: "var(--ink-3)" }}>
                                {voice.isVoiceAvailable ? "Speak aloud" : "Unavailable"}
                            </span>
                        </button>
                    </div>
                </div>
            )}

            {/* Chat messages */}
            {mode !== null && (
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
                    {messages.length === 0 && (
                        <p style={{ fontSize: 13, color: "var(--ink-4)", textAlign: "center", margin: "auto" }}>
                            {mode === "voice" ? "Tap the mic and start the conversation" : "Write your first line"}
                        </p>
                    )}
                    {messages.map((message, idx) => (
                        <div
                            key={idx}
                            style={{
                                display: "flex",
                                justifyContent: message.role === "user" ? "flex-end" : "flex-start",
                            }}
                        >
                            <div
                                style={{
                                    maxWidth: "80%",
                                    padding: "10px 14px",
                                    borderRadius: message.role === "user" ? "14px 14px 4px 14px" : "4px 14px 14px 14px",
                                    background: message.role === "user" ? "var(--primary)" : "var(--bg-2)",
                                    color: message.role === "user" ? "white" : "var(--ink)",
                                    fontSize: 14,
                                    lineHeight: 1.4,
                                }}
                            >
                                {message.content}
                            </div>
                        </div>
                    ))}
                    {(isSending || voice.state === "processing") && (
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
                                <span style={{ opacity: 0.7 }}>Typing...</span>
                            </div>
                        </div>
                    )}
                    <div ref={messagesEndRef} />
                </div>
            )}

            {/* Text input */}
            {showInput && (
                <div style={{ display: "flex", gap: 8 }}>
                    <input
                        type="text"
                        value={inputText}
                        onChange={(e) => setInputText(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder="Your line…"
                        disabled={isSending}
                        autoFocus
                        style={{
                            flex: 1,
                            padding: "10px 14px",
                            border: "1px solid var(--line-2)",
                            borderRadius: 10,
                            fontFamily: "var(--font-ui)",
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

            {/* Voice controls */}
            {showVoiceControls && (
                <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 10, padding: "8px 0" }}>
                    {voice.currentTranscript && (
                        <p style={{ fontSize: 13, color: "var(--ink-3)", margin: 0, fontStyle: "italic" }}>
                            {voice.currentTranscript}
                        </p>
                    )}
                    <VoiceMicButton
                        state={voice.state}
                        isAvailable={voice.isVoiceAvailable}
                        onStart={voice.startVoice}
                        onStop={voice.stopVoice}
                    />
                    {voiceError && (
                        <p style={{ fontSize: 12, color: "var(--heart)", margin: 0 }}>{voiceError}</p>
                    )}
                </div>
            )}

            {mode !== null && !isAnswered && !canComplete && (
                <p style={{ fontSize: 12, color: "var(--ink-4)", textAlign: "center", margin: 0 }}>
                    {minTurns - userTurnCount} more line{minTurns - userTurnCount === 1 ? "" : "s"} before you can finish
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
                                SKIP
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
                                FINISH DIALOGUE
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

const modeChoiceStyle: React.CSSProperties = {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 6,
    padding: "20px 16px",
    background: "var(--surface)",
    border: "1px solid var(--line-2)",
    borderRadius: 14,
    cursor: "pointer",
    color: "var(--ink)",
    fontFamily: "var(--font-ui)",
};
