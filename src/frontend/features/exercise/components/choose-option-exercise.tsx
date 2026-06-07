"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { useKeyboardControls } from "@/shared/hooks/use-keyboard-controls";
import { Icon } from "@/shared/components/icon";
import { GeoAvatar } from "@/shared/components/geo-avatar";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

interface ChooseOptionContent {
    situation: string;
    options: Array<{ text: string; is_correct: boolean }>;
    explanation?: string;
}

interface ChooseOptionExerciseProps {
    content: ChooseOptionContent;
    onSubmit: (answer: { selectedOptionIndex: number }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function ChooseOptionExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: ChooseOptionExerciseProps) {
    const [selectedOptionIndex, setSelectedOptionIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    useKeyboardControls({
        optionCount: content.options.length,
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

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            {/* Context bubble */}
            {content.situation && (
                <div style={{ display: "flex", gap: 16, marginBottom: 8 }}>
                    <GeoAvatar seed="prospect" size={56} />
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
                            {content.situation}
                        </div>
                    </div>
                </div>
            )}

            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                Выберите лучший ответ:
            </h2>

            <div className="col gap-3">
                {content.options.map((option, optionIndex) => {
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
                            {(showSuccess || showCorrect) && (
                                <Icon name="check" size={20} style={{ color: "var(--success)", flex: "none" }} />
                            )}
                            {showWrong && (
                                <Icon name="close" size={20} style={{ color: "var(--heart)", flex: "none" }} />
                            )}
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