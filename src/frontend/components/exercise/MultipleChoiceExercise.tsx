"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { useKeyboardControls } from "@/lib/hooks/useKeyboardControls";
import { Icon } from "@/components/ui/Icon";

interface MultipleChoiceContent {
    situation: string;
    question: string;
    options: string[];
    correctOptionIndex: number;
    explanation?: string;
}

interface MultipleChoiceExerciseProps {
    content: MultipleChoiceContent;
    onSubmit: (answer: { selectedOptionIndex: number }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function MultipleChoiceExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: MultipleChoiceExerciseProps) {
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

    function optionStyle(optionIndex: number): string {
        const base = "flex items-center gap-4 px-4 py-4 rounded-2xl text-left font-semibold transition-colors border-b-4";

        if (!isAnswered) {
            return `${base} ${
                selectedOptionIndex === optionIndex
                    ? "border-tertiary bg-tertiary-container text-tertiary"
                    : "border-outline-variant bg-surface-container text-on-surface hover:bg-surface-container-high"
            }`;
        }

        const isSelected = selectedOptionIndex === optionIndex;
        const isCorrectOption = optionIndex === content.correctOptionIndex;

        if (isSelected && submittedResult.isCorrect) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        if (isSelected && !submittedResult.isCorrect) {
            return `${base} border-error bg-error-container text-error`;
        }
        if (!submittedResult.isCorrect && isCorrectOption) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        return `${base} border-outline-variant bg-surface-container text-on-surface-variant`;
    }

    function badgeStyle(optionIndex: number): string {
        const base = "w-7 h-7 rounded-full flex items-center justify-center text-sm font-extrabold shrink-0";

        if (!isAnswered) {
            return `${base} ${
                selectedOptionIndex === optionIndex
                    ? "bg-tertiary text-on-tertiary"
                    : "bg-surface-container-highest text-on-surface-variant"
            }`;
        }

        const isSelected = selectedOptionIndex === optionIndex;
        const isCorrectOption = optionIndex === content.correctOptionIndex;

        if (isSelected && submittedResult.isCorrect) return `${base} bg-primary text-on-primary`;
        if (isSelected && !submittedResult.isCorrect) return `${base} bg-error text-on-error`;
        if (!submittedResult.isCorrect && isCorrectOption) return `${base} bg-primary text-on-primary`;
        return `${base} bg-surface-container-highest text-on-surface-variant`;
    }

    return (
        <div className="flex flex-col gap-6">
            {/* Character speech bubble */}
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="handshake" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-xl text-on-surface">{content.question}</p>

            <div className="flex flex-col gap-3">
                {content.options.map((optionText, optionIndex) => (
                    <button
                        key={optionIndex}
                        onClick={() => {
                            if (!isAnswered) setSelectedOptionIndex(optionIndex);
                        }}
                        disabled={isAnswered}
                        className={optionStyle(optionIndex)}
                    >
                        <span className={badgeStyle(optionIndex)}>{optionIndex + 1}</span>
                        {optionText}
                    </button>
                ))}
            </div>

            {/* Inline explanation */}
            {isAnswered && (submittedResult.explanation || submittedResult.aiFeedback) && (
                <p className={`text-sm leading-relaxed px-1 ${
                    submittedResult.isCorrect ? "text-primary" : "text-error"
                }`}>
                    {submittedResult.explanation ?? submittedResult.aiFeedback}
                </p>
            )}

            <div className="flex gap-3">
                {!isAnswered && onSkip && (
                    <button
                        onClick={onSkip}
                        disabled={isSubmitting}
                        className="flex-1 py-4 rounded-full border-2 border-outline-variant text-on-surface-variant font-extrabold hover:border-outline hover:text-on-surface transition-colors disabled:opacity-40"
                    >
                        Пропустить
                    </button>
                )}

                {isAnswered ? (
                    <button
                        onClick={onContinue}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d"
                    >
                        Продолжить
                    </button>
                ) : (
                    <button
                        onClick={() => {
                            if (selectedOptionIndex !== null) onSubmit({ selectedOptionIndex });
                        }}
                        disabled={selectedOptionIndex === null || isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                        {isSubmitting ? "Проверяем..." : "Проверить"}
                    </button>
                )}
            </div>

            {/* Keyboard hint — hidden on touch devices */}
            <p className="hidden pointer-fine:block text-center text-xs text-on-surface-variant">
                {isAnswered ? "Enter — продолжить" : "1–4 выбрать · Enter — проверить"}
            </p>
        </div>
    );
}
