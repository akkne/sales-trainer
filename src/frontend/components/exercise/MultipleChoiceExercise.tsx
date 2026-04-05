"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { useKeyboardControls } from "@/lib/hooks/useKeyboardControls";

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
                    ? "border-[#1CB0F6] bg-[#E8F7FE] text-[#1CB0F6]"
                    : "border-[#E5E5E5] bg-[#F7F7F7] text-gray-700 hover:bg-[#EEFAFF]"
            }`;
        }

        // After submission: colour by correctness
        const isSelected = selectedOptionIndex === optionIndex;
        const isCorrectOption = optionIndex === content.correctOptionIndex;

        if (isSelected && submittedResult.isCorrect) {
            // Correct answer selected → green
            return `${base} border-[#58CC02] bg-[#E8F9D6] text-[#3C8400]`;
        }
        if (isSelected && !submittedResult.isCorrect) {
            // Wrong answer selected → red
            return `${base} border-[#FF4B4B] bg-[#FFF2F2] text-[#CC3333]`;
        }
        if (!submittedResult.isCorrect && isCorrectOption) {
            // Show the correct answer in green when user was wrong
            return `${base} border-[#58CC02] bg-[#E8F9D6] text-[#3C8400]`;
        }
        // All other options: faded
        return `${base} border-[#E5E5E5] bg-[#F7F7F7] text-gray-400`;
    }

    function badgeStyle(optionIndex: number): string {
        const base = "w-7 h-7 rounded-full flex items-center justify-center text-sm font-extrabold shrink-0";

        if (!isAnswered) {
            return `${base} ${
                selectedOptionIndex === optionIndex
                    ? "bg-[#1CB0F6] text-white"
                    : "bg-[#E5E5E5] text-gray-500"
            }`;
        }

        const isSelected = selectedOptionIndex === optionIndex;
        const isCorrectOption = optionIndex === content.correctOptionIndex;

        if (isSelected && submittedResult.isCorrect) return `${base} bg-[#58CC02] text-white`;
        if (isSelected && !submittedResult.isCorrect) return `${base} bg-[#FF4B4B] text-white`;
        if (!submittedResult.isCorrect && isCorrectOption) return `${base} bg-[#58CC02] text-white`;
        return `${base} bg-[#E5E5E5] text-gray-400`;
    }

    return (
        <div className="flex flex-col gap-6">
            {/* Character speech bubble */}
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-[#58CC02] flex items-center justify-center text-white font-bold text-lg shrink-0 mt-1">
                        🤝
                    </div>
                    <div className="relative bg-[#F7F7F7] rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-gray-700">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-extrabold text-xl text-gray-900">{content.question}</p>

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
                    submittedResult.isCorrect ? "text-[#3C8400]" : "text-[#CC3333]"
                }`}>
                    {submittedResult.explanation ?? submittedResult.aiFeedback}
                </p>
            )}

            <div className="flex gap-3">
                {!isAnswered && onSkip && (
                    <button
                        onClick={onSkip}
                        disabled={isSubmitting}
                        className="flex-1 py-4 rounded-2xl border-2 border-[#E5E5E5] text-[#AFAFAF] font-extrabold hover:border-gray-300 hover:text-gray-500 transition-colors disabled:opacity-40"
                    >
                        Пропустить
                    </button>
                )}

                {isAnswered ? (
                    <button
                        onClick={onContinue}
                        className="flex-1 py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d"
                    >
                        Продолжить
                    </button>
                ) : (
                    <button
                        onClick={() => {
                            if (selectedOptionIndex !== null) onSubmit({ selectedOptionIndex });
                        }}
                        disabled={selectedOptionIndex === null || isSubmitting}
                        className="flex-1 py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                        {isSubmitting ? "Проверяем..." : "Проверить"}
                    </button>
                )}
            </div>

            {/* Keyboard hint — hidden on touch devices */}
            <p className="hidden pointer-fine:block text-center text-xs text-[#AFAFAF]">
                {isAnswered ? "Enter — продолжить" : "1–4 выбрать · Enter — проверить"}
            </p>
        </div>
    );
}
