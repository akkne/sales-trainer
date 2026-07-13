"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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
            <div><span className="ex-chip ex-chip--evaluate">Оцени звонок</span></div>
            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                Оцени звонок по критериям:
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
                        fontFamily: "var(--font-ui)",
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
                            fontFamily: "var(--font-mono)",
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
                                        background: ratings[axis.name] >= value ? "var(--amber)" : "var(--bg-2)",
                                        color: ratings[axis.name] >= value ? "white" : "var(--ink-4)",
                                        fontSize: 14,
                                        fontFamily: "var(--font-mono)",
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
                        Общий комментарий (необязательно):
                    </label>
                    <textarea
                        value={overallComment}
                        onChange={(e) => setOverallComment(e.target.value)}
                        placeholder="Твои наблюдения о звонке..."
                        style={{
                            width: "100%",
                            padding: 16,
                            borderRadius: 12,
                            background: "var(--surface)",
                            border: "1px solid var(--line)",
                            outline: "none",
                            resize: "none",
                            minHeight: 80,
                            fontFamily: "var(--font-ui)",
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
                <ExerciseActionFooter
                    onSkip={onSkip}
                    onSubmit={() => onSubmit({ ratings, overallComment: overallComment || undefined })}
                    submitLabel="Отправить оценку"
                    canSubmit={allRated}
                    isSubmitting={isSubmitting}
                />
            )}
        </div>
    );
}
