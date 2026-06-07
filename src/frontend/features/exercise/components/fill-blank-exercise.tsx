"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { useKeyboardControls } from "@/shared/hooks/use-keyboard-controls";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

interface FillBlankContent {
    before: string;
    after: string;
    options: Array<{ text: string; is_correct: boolean }>;
    explanation?: string;
}

interface FillBlankExerciseProps {
    content: FillBlankContent;
    onSubmit: (answer: { selectedOptionIndex: number }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function FillBlankExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: FillBlankExerciseProps) {
    const [selectedOptionIndex, setSelectedOptionIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    useKeyboardControls({
        optionCount: content.options?.length ?? 0,
        onSelectOption: (index) => {
            if (!isAnswered) setSelectedOptionIndex(index);
        },
        onSubmit: () => {
            if (selectedOptionIndex !== null && !isSubmitting) {
                onSubmit({ selectedOptionIndex });
            }
        },
        onContinue: () => onContinue?.(),
        isAnswered,
        disabled: isSubmitting,
    });

    const selectedText = selectedOptionIndex !== null ? content.options[selectedOptionIndex]?.text : null;

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            {/* Context bubble */}
            <div style={{ display: "flex", gap: 16, marginBottom: 8 }}>
                <GeoAvatar seed="client" size={56} />
                <div style={{ flex: 1 }}>
                    <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 6 }}>
                        <span style={{ fontSize: 13, fontWeight: 500 }}>Клиент</span>
                        <span style={{ fontSize: 11, color: "var(--ink-3)", fontFamily: "var(--font-mono)" }}>prospect</span>
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
                            fontFamily: "var(--font-ui)",
                        }}
                    >
                        {content.before}
                    </div>
                </div>
            </div>

            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                Дополните реплику:
            </h2>

            {/* Fill blank sentence */}
            <div
                style={{
                    fontSize: 22,
                    lineHeight: 1.6,
                    letterSpacing: -0.2,
                    padding: 24,
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: "var(--r-md)",
                    marginBottom: 4,
                }}
            >
                «{content.before}{" "}
                <span
                    style={{
                        display: "inline-block",
                        minWidth: 180,
                        padding: "4px 12px",
                        borderRadius: 8,
                        background: selectedText ? "var(--primary-soft)" : "var(--bg-2)",
                        color: selectedText ? "var(--primary-strong)" : "var(--ink-4)",
                        border: selectedText ? "1px dashed var(--primary)" : "1px dashed var(--line-2)",
                        textAlign: "center",
                        fontSize: 18,
                    }}
                >
                    {selectedText ?? "???"}
                </span>{" "}
                {content.after}»
            </div>

            {/* Options grid */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                {(content.options ?? []).map((option, optionIndex) => {
                    const isSelected = selectedOptionIndex === optionIndex;
                    const isCorrectOption = option.is_correct;
                    const showCorrect = isAnswered && !submittedResult?.isCorrect && isCorrectOption;
                    const showWrong = isAnswered && isSelected && !submittedResult?.isCorrect;
                    const showSuccess = isAnswered && isSelected && submittedResult?.isCorrect;

                    let cls = "opt";
                    if (showSuccess || showCorrect) cls += " correct";
                    else if (showWrong) cls += " wrong";
                    else if (isAnswered) cls += " dim";
                    else if (isSelected) cls += " sel";

                    return (
                        <button
                            key={optionIndex}
                            className={cls}
                            onClick={() => {
                                if (!isAnswered) setSelectedOptionIndex(optionIndex);
                            }}
                            disabled={isAnswered}
                        >
                            <span className="opt-key">{String.fromCharCode(1040 + optionIndex)}</span>
                            <span className="opt-text">{option.text}</span>
                        </button>
                    );
                })}
            </div>

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
                    onSubmit={() => {
                        if (selectedOptionIndex !== null) onSubmit({ selectedOptionIndex });
                    }}
                    canSubmit={selectedOptionIndex !== null}
                    isSubmitting={isSubmitting}
                    keyboardHint="1–4 выбрать · Enter — проверить"
                />
            )}
        </div>
    );
}
