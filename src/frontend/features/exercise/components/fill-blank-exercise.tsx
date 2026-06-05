"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { useKeyboardControls } from "@/shared/hooks/use-keyboard-controls";
import { Icon } from "@/shared/components/icon";

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
                        {content.before}
                    </div>
                </div>
            </div>

            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
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
                    borderRadius: 14,
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
                        background: selectedText ? "var(--indigo-soft)" : "var(--bg-2)",
                        color: selectedText ? "var(--indigo-ink)" : "var(--ink-4)",
                        border: selectedText ? "1px dashed var(--indigo)" : "1px dashed var(--line-2)",
                        textAlign: "center",
                        fontSize: 18,
                    }}
                >
                    {selectedText ?? "???"}
                </span>{" "}
                {content.after}»
            </div>

            {/* Options grid */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                {(content.options ?? []).map((option, optionIndex) => {
                    const isSelected = selectedOptionIndex === optionIndex;
                    const isCorrectOption = option.is_correct;
                    const showCorrect = isAnswered && !submittedResult?.isCorrect && isCorrectOption;
                    const showWrong = isAnswered && isSelected && !submittedResult?.isCorrect;
                    const showSuccess = isAnswered && isSelected && submittedResult?.isCorrect;

                    let bgColor = isSelected ? "var(--ink)" : "var(--surface)";
                    let textColor = isSelected ? "var(--bg)" : "var(--ink)";
                    let borderColor = isSelected ? "var(--ink)" : "var(--line)";
                    let badgeBg = isSelected ? "var(--bg)" : "var(--bg-2)";
                    let badgeColor = isSelected ? "var(--ink)" : "var(--ink-3)";

                    if (showSuccess) {
                        bgColor = "var(--good-soft)";
                        textColor = "var(--ink)";
                        borderColor = "var(--good)";
                        badgeBg = "var(--good)";
                        badgeColor = "white";
                    } else if (showWrong) {
                        bgColor = "var(--bad-soft)";
                        textColor = "var(--ink)";
                        borderColor = "var(--bad)";
                        badgeBg = "var(--bad)";
                        badgeColor = "white";
                    } else if (showCorrect) {
                        bgColor = "var(--good-soft)";
                        textColor = "var(--ink)";
                        borderColor = "var(--good)";
                        badgeBg = "var(--good)";
                        badgeColor = "white";
                    }

                    return (
                        <button
                            key={optionIndex}
                            onClick={() => {
                                if (!isAnswered) setSelectedOptionIndex(optionIndex);
                            }}
                            disabled={isAnswered}
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 14,
                                padding: "14px 18px",
                                background: bgColor,
                                color: textColor,
                                border: `1px solid ${borderColor}`,
                                borderRadius: 12,
                                cursor: isAnswered ? "default" : "pointer",
                                textAlign: "left",
                                fontSize: 15,
                                fontFamily: "var(--f-sans)",
                                fontWeight: 400,
                                lineHeight: 1.4,
                                transition: "all 0.12s",
                                boxShadow: isSelected && !isAnswered ? "var(--sh-2)" : "var(--sh-1)",
                            }}
                        >
                            <span
                                style={{
                                    width: 26,
                                    height: 26,
                                    borderRadius: 7,
                                    background: badgeBg,
                                    color: badgeColor,
                                    display: "inline-flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    fontSize: 12,
                                    fontFamily: "var(--f-mono)",
                                    fontWeight: 500,
                                    flexShrink: 0,
                                }}
                            >
                                {optionIndex + 1}
                            </span>
                            <span>{option.text}</span>
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
                            <div
                                className="mono"
                                style={{ fontSize: 11, color: "var(--ink-4)", display: "none" }}
                                data-keyboard-hint
                            >
                                1–4 выбрать · Enter — проверить
                            </div>
                            <Button
                                variant="accent"
                                size="lg"
                                onClick={() => {
                                    if (selectedOptionIndex !== null) onSubmit({ selectedOptionIndex });
                                }}
                                disabled={selectedOptionIndex === null || isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ПРОВЕРИТЬ
                            </Button>
                        </div>
                    </div>
                    <style jsx global>{`
                        @media (pointer: fine) {
                            [data-keyboard-hint] {
                                display: block !important;
                            }
                        }
                    `}</style>
                </div>
            )}
        </div>
    );
}
