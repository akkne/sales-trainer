"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { ExerciseResultBanner } from "./exercise-result-banner";

interface DialogueLine {
    speaker: string;
    text: string;
    is_mistake: boolean;
}

interface SpotMistakeContent {
    dialogue: DialogueLine[];
    explanation?: string;
    ai_prompt?: string;
}

interface SpotMistakeExerciseProps {
    content: SpotMistakeContent;
    onSubmit: (answer: { selectedLineIndex: number; explanation?: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function SpotMistakeExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: SpotMistakeExerciseProps) {
    const [selectedLineIndex, setSelectedLineIndex] = useState<number | null>(null);
    const [explanation, setExplanation] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const mistakeIndex = content.dialogue.findIndex(line => line.is_mistake);

    function getLineStyle(lineIndex: number) {
        const isSelected = selectedLineIndex === lineIndex;
        const isMistakeLine = lineIndex === mistakeIndex;
        const wasSelected = selectedLineIndex === lineIndex;

        if (!isAnswered) {
            return {
                background: isSelected ? "var(--bad-soft)" : "transparent",
                border: `1px solid ${isSelected ? "var(--bad)" : "transparent"}`,
            };
        }

        if (wasSelected && isMistakeLine) {
            return { background: "var(--good-soft)", border: "1px solid var(--good)" };
        }
        if (wasSelected && !isMistakeLine) {
            return { background: "var(--bad-soft)", border: "1px solid var(--bad)" };
        }
        if (isMistakeLine) {
            return { background: "var(--good-soft)", border: "1px solid var(--good)", opacity: 0.7 };
        }
        return { background: "transparent", border: "1px solid transparent" };
    }

    const canSubmit = selectedLineIndex !== null;

    function handleSubmit() {
        if (selectedLineIndex === null) return;
        onSubmit({
            selectedLineIndex,
            explanation: explanation.length > 10 ? explanation : undefined,
        });
    }

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
                Найдите реплику, где продавец допустил ошибку:
            </h2>

            <div
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                    padding: 8,
                }}
            >
                {content.dialogue.map((line, index) => {
                    const lineStyle = getLineStyle(index);
                    return (
                        <button
                            key={index}
                            onClick={() => !isAnswered && setSelectedLineIndex(index)}
                            disabled={isAnswered}
                            style={{
                                width: "100%",
                                textAlign: "left",
                                padding: "12px 14px",
                                margin: "2px 0",
                                background: lineStyle.background,
                                border: lineStyle.border,
                                borderRadius: 10,
                                cursor: isAnswered ? "default" : "pointer",
                                fontSize: 14,
                                fontFamily: "var(--f-sans)",
                                display: "flex",
                                gap: 10,
                                opacity: (lineStyle as { opacity?: number }).opacity ?? 1,
                            }}
                        >
                            <span
                                style={{
                                    fontSize: 11,
                                    fontFamily: "var(--f-mono)",
                                    color: "var(--ink-4)",
                                    marginTop: 2,
                                    width: 28,
                                    flexShrink: 0,
                                }}
                            >
                                {line.speaker}
                            </span>
                            <span>{line.text}</span>
                        </button>
                    );
                })}
            </div>

            {selectedLineIndex !== null && !isAnswered && (
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    <label style={{ fontWeight: 500, fontSize: 14 }}>
                        Объясните, почему это ошибка (опционально):
                    </label>
                    <textarea
                        value={explanation}
                        onChange={(e) => setExplanation(e.target.value)}
                        placeholder="Напишите ваше объяснение..."
                        style={{
                            width: "100%",
                            padding: 16,
                            borderRadius: 12,
                            background: "var(--surface)",
                            border: "1px solid var(--line)",
                            outline: "none",
                            resize: "none",
                            minHeight: 100,
                            fontFamily: "var(--f-sans)",
                            fontSize: 14,
                        }}
                    />
                </div>
            )}

            {isAnswered ? (
                <ExerciseResultBanner
                    isCorrect={submittedResult.isCorrect}
                    score={submittedResult.score}
                    explanation={submittedResult.explanation ?? null}
                    aiFeedback={submittedResult.aiFeedback ?? null}
                    xpEarned={submittedResult.xpEarned}
                    onContinue={onContinue ?? (() => {})}
                />
            ) : (
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
                                onClick={handleSubmit}
                                disabled={!canSubmit || isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ПРОВЕРИТЬ
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
