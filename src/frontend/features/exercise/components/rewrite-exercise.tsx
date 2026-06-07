"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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
            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
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
                        fontFamily: "var(--font-ui)",
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
                        fontFamily: "var(--font-mono)",
                    }}
                >
                    <span>
                        {charCount < minLength ? (
                            <span style={{ color: "var(--amber)" }}>минимум {minLength} · сейчас {charCount}</span>
                        ) : (
                            <span style={{ color: "var(--success)" }}>{charCount} символов ✓</span>
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
                        background: "var(--heart-soft)",
                        border: "1px solid var(--heart)",
                    }}
                >
                    <p style={{ margin: 0, fontSize: 14, color: "var(--heart)" }}>
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
                <ExerciseActionFooter
                    onSkip={onSkip}
                    onSubmit={() => onSubmit({ rewrittenText })}
                    submitLabel="Отправить"
                    canSubmit={isValidLength}
                    isSubmitting={isSubmitting}
                />
            )}
        </div>
    );
}
