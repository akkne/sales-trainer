"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { ExerciseResultBanner } from "./exercise-result-banner";

interface RewriteContent {
    instruction: string;
    original: string;
    evaluation_criteria?: string[];
    ai_prompt?: string;
}

interface RewriteExerciseProps {
    content: RewriteContent;
    onSubmit: (answer: { rewrittenText: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
    submitError?: Error | null;
}

export function RewriteExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
    submitError,
}: RewriteExerciseProps) {
    const [rewrittenText, setRewrittenText] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const minLength = 20;
    const charCount = rewrittenText.length;
    const isValidLength = charCount >= minLength;

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
                {content.instruction || "Перепишите реплику лучше:"}
            </h2>

            <div
                style={{
                    padding: 18,
                    background: "var(--bg-2)",
                    borderRadius: 14,
                    color: "var(--ink-3)",
                    fontSize: 15,
                    lineHeight: 1.5,
                    fontStyle: "italic",
                }}
            >
                «{content.original}»
            </div>

            <div
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                    padding: 16,
                }}
            >
                <textarea
                    placeholder="Ваш вариант…"
                    value={rewrittenText}
                    onChange={(e) => setRewrittenText(e.target.value)}
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

            {submitError && !isAnswered && (
                <div
                    style={{
                        padding: 16,
                        borderRadius: 12,
                        background: "var(--bad-soft)",
                        border: "1px solid var(--bad)",
                    }}
                >
                    <p style={{ margin: 0, fontSize: 14, color: "var(--bad)" }}>
                        Произошла ошибка при проверке. Попробуйте ещё раз.
                    </p>
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
                                onClick={() => onSubmit({ rewrittenText })}
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
