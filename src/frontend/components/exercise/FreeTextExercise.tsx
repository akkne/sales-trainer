"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { GeoAvatar } from "@/components/ui/GeoAvatar";
import { ExerciseResultBanner } from "./ExerciseResultBanner";

interface FreeTextContent {
    situation?: string;
    instruction: string;
    evaluation_criteria?: string[];
    ai_prompt?: string;
}

interface FreeTextExerciseProps {
    content: FreeTextContent;
    onSubmit: (answer: { text: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function FreeTextExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: FreeTextExerciseProps) {
    const [text, setText] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const minLength = 20;
    const charCount = text.length;
    const isValidLength = charCount >= minLength;

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            {content.situation && (
                <div style={{ display: "flex", gap: 16, marginBottom: 8 }}>
                    <GeoAvatar seed="client" size={56} />
                    <div style={{ flex: 1 }}>
                        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 6 }}>
                            <span style={{ fontSize: 13, fontWeight: 500 }}>Клиент</span>
                            <span style={{ fontSize: 11, color: "var(--ink-3)", fontFamily: "var(--f-mono)" }}>prospect</span>
                        </div>
                        <div
                            style={{
                                position: "relative",
                                background: "var(--surface)",
                                border: "1px solid var(--line)",
                                borderRadius: "4px 14px 14px 14px",
                                padding: "14px 18px",
                                fontSize: 15,
                                lineHeight: 1.5,
                                boxShadow: "var(--sh-1)",
                            }}
                        >
                            {content.situation}
                        </div>
                    </div>
                </div>
            )}

            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
                {content.instruction}
            </h2>

            <div
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                    padding: 16,
                }}
            >
                <textarea
                    placeholder="Минимум 20 символов…"
                    value={text}
                    onChange={(e) => setText(e.target.value)}
                    disabled={isAnswered}
                    rows={5}
                    style={{
                        width: "100%",
                        border: "none",
                        outline: "none",
                        resize: "vertical",
                        background: "transparent",
                        fontFamily: "var(--f-sans)",
                        fontSize: 15,
                        color: "var(--ink)",
                        lineHeight: 1.5,
                    }}
                />
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        marginTop: 8,
                        fontSize: 12,
                        color: "var(--ink-3)",
                        fontFamily: "var(--f-mono)",
                    }}
                >
                    <span>
                        {charCount < minLength ? (
                            <span style={{ color: "var(--warn)" }}>минимум {minLength} · сейчас {charCount}</span>
                        ) : (
                            <span style={{ color: "var(--good)" }}>{charCount} символов ✓</span>
                        )}
                    </span>
                    <button
                        type="button"
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: 6,
                            background: "var(--bg-2)",
                            border: "1px solid var(--line)",
                            borderRadius: 8,
                            padding: "4px 10px",
                            color: "var(--ink-2)",
                            cursor: "pointer",
                            fontSize: 12,
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        <Icon name="mic" size="xs" /> Голосом
                    </button>
                </div>
            </div>

            {content.evaluation_criteria && content.evaluation_criteria.length > 0 && (
                <div
                    style={{
                        padding: 16,
                        borderRadius: 12,
                        background: "var(--surface)",
                        border: "1px solid var(--line)",
                    }}
                >
                    <div
                        style={{
                            fontSize: 11,
                            color: "var(--ink-3)",
                            textTransform: "uppercase",
                            letterSpacing: 1,
                            marginBottom: 8,
                            fontWeight: 500,
                        }}
                    >
                        Критерии оценки
                    </div>
                    <ul style={{ margin: 0, paddingLeft: 18, color: "var(--ink-2)", fontSize: 13, lineHeight: 1.8 }}>
                        {content.evaluation_criteria.map((criterion, idx) => (
                            <li key={idx}>{criterion}</li>
                        ))}
                    </ul>
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
                                onClick={() => onSubmit({ text })}
                                disabled={!isValidLength || isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ОТПРАВИТЬ
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
