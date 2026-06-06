"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { ExerciseResultBanner } from "./exercise-result-banner";

interface TranscriptLine {
    speaker: string;
    text: string;
}

interface EvaluationAxis {
    name: string;
    description: string;
}

interface EvaluateCallContent {
    transcript: TranscriptLine[];
    evaluation_axes: EvaluationAxis[];
    ai_prompt?: string;
}

interface EvaluateCallExerciseProps {
    content: EvaluateCallContent;
    onSubmit: (answer: { ratings: Record<string, number>; overallComment?: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function EvaluateCallExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: EvaluateCallExerciseProps) {
    const [ratings, setRatings] = useState<Record<string, number>>({});
    const [overallComment, setOverallComment] = useState("");
    const [showTranscript, setShowTranscript] = useState(true);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const allRated = content.evaluation_axes.every(axis => ratings[axis.name] !== undefined);

    function setRating(axisName: string, value: number) {
        setRatings({ ...ratings, [axisName]: value });
    }

    const ratingOptions = [1, 2, 3, 4, 5];

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
                Оцените звонок по критериям:
            </h2>

            {/* Transcript toggle */}
            <div
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                    overflow: "hidden",
                }}
            >
                <button
                    type="button"
                    onClick={() => setShowTranscript(!showTranscript)}
                    style={{
                        width: "100%",
                        padding: 16,
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        background: "var(--bg-2)",
                        border: "none",
                        cursor: "pointer",
                        fontSize: 13,
                        fontFamily: "var(--f-sans)",
                        color: "var(--ink-2)",
                    }}
                >
                    <span>
                        <Icon name="phone" size="sm" style={{ verticalAlign: -2, marginRight: 8 }} />
                        Транскрипт звонка
                    </span>
                    <Icon name={showTranscript ? "chevron-up" : "chevron-down"} size="sm" />
                </button>

                {showTranscript && (
                    <div
                        style={{
                            padding: 16,
                            fontSize: 13,
                            color: "var(--ink-2)",
                            lineHeight: 1.6,
                            maxHeight: 280,
                            overflow: "auto",
                            fontFamily: "var(--f-mono)",
                        }}
                    >
                        {content.transcript.map((line, idx) => (
                            <div key={idx} style={{ display: "flex", gap: 8, marginBottom: 4 }}>
                                <span
                                    style={{
                                        fontWeight: 600,
                                        fontSize: 12,
                                        flexShrink: 0,
                                        width: 80,
                                        color: "var(--ink-3)",
                                        textTransform: "capitalize",
                                    }}
                                >
                                    {line.speaker}:
                                </span>
                                <span style={{ color: "var(--ink)" }}>{line.text}</span>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* Evaluation axes ratings */}
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {content.evaluation_axes.map((axis) => (
                    <div
                        key={axis.name}
                        style={{
                            padding: 14,
                            background: "var(--surface)",
                            border: "1px solid var(--line)",
                            borderRadius: 12,
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                        }}
                    >
                        <div>
                            <div style={{ fontSize: 14, fontWeight: 500 }}>{axis.name}</div>
                            {axis.description && (
                                <div style={{ fontSize: 12, color: "var(--ink-3)" }}>{axis.description}</div>
                            )}
                        </div>
                        <div style={{ display: "flex", gap: 6 }}>
                            {ratingOptions.map((value) => (
                                <button
                                    key={value}
                                    onClick={() => !isAnswered && setRating(axis.name, value)}
                                    disabled={isAnswered}
                                    style={{
                                        width: 34,
                                        height: 34,
                                        borderRadius: 10,
                                        border: "none",
                                        cursor: isAnswered ? "default" : "pointer",
                                        background: ratings[axis.name] >= value ? "var(--rust)" : "var(--bg-2)",
                                        color: ratings[axis.name] >= value ? "white" : "var(--ink-4)",
                                        fontSize: 14,
                                        fontFamily: "var(--f-mono)",
                                        fontWeight: 500,
                                    }}
                                >
                                    ★
                                </button>
                            ))}
                        </div>
                    </div>
                ))}
            </div>

            {/* Overall comment */}
            {!isAnswered && (
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    <label style={{ fontWeight: 500, fontSize: 14 }}>
                        Общий комментарий (опционально):
                    </label>
                    <textarea
                        value={overallComment}
                        onChange={(e) => setOverallComment(e.target.value)}
                        placeholder="Ваши наблюдения о звонке..."
                        style={{
                            width: "100%",
                            padding: 16,
                            borderRadius: 12,
                            background: "var(--surface)",
                            border: "1px solid var(--line)",
                            outline: "none",
                            resize: "none",
                            minHeight: 80,
                            fontFamily: "var(--f-sans)",
                            fontSize: 14,
                        }}
                    />
                </div>
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
                                onClick={() => onSubmit({ ratings, overallComment: overallComment || undefined })}
                                disabled={!allRated || isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ОТПРАВИТЬ ОЦЕНКУ
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
