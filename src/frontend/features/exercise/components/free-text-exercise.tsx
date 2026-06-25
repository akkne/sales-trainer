"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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
            {/* Exercise type chip */}
            <div><span className="ex-chip ex-chip--free">Свободный ответ</span></div>

            {/* Context bubble — client speech */}
            {content.situation && (
                <div style={{ display: "flex", gap: 14, marginBottom: 4 }}>
                    <GeoAvatar seed="client" size={48} />
                    <div style={{ flex: 1 }}>
                        <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 5 }}>
                            <span style={{ fontSize: 13, fontWeight: 600, color: "var(--ink-2)" }}>Клиент</span>
                        </div>
                        <div className="ex-bubble-client">
                            {content.situation}
                        </div>
                    </div>
                </div>
            )}

            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
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
                            fontFamily: "var(--font-mono)",
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
                <ExerciseActionFooter
                    onSkip={onSkip}
                    onSubmit={() => onSubmit({ text })}
                    submitLabel="Отправить"
                    canSubmit={isValidLength}
                    isSubmitting={isSubmitting}
                />
            )}
        </div>
    );
}
