"use client";

import { useState } from "react";

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
    isSubmitting: boolean;
}

export function MultipleChoiceExercise({
    content,
    onSubmit,
    isSubmitting,
}: MultipleChoiceExerciseProps) {
    const [selectedOptionIndex, setSelectedOptionIndex] = useState<number | null>(null);

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
                {content.options.map((optionText, optionIndex) => {
                    const isSelected = selectedOptionIndex === optionIndex;
                    return (
                        <button
                            key={optionIndex}
                            onClick={() => setSelectedOptionIndex(optionIndex)}
                            className={`flex items-center gap-4 px-4 py-4 rounded-2xl text-left font-semibold transition-colors border-b-4 ${
                                isSelected
                                    ? "border-[#1CB0F6] bg-[#E8F7FE] text-[#1CB0F6]"
                                    : "border-[#E5E5E5] bg-[#F7F7F7] text-gray-700 hover:bg-[#EEFAFF]"
                            }`}
                        >
                            <span
                                className={`w-7 h-7 rounded-full flex items-center justify-center text-sm font-extrabold shrink-0 ${
                                    isSelected
                                        ? "bg-[#1CB0F6] text-white"
                                        : "bg-[#E5E5E5] text-gray-500"
                                }`}
                            >
                                {optionIndex + 1}
                            </span>
                            {optionText}
                        </button>
                    );
                })}
            </div>

            <button
                onClick={() => {
                    if (selectedOptionIndex !== null) onSubmit({ selectedOptionIndex });
                }}
                disabled={selectedOptionIndex === null || isSubmitting}
                className="py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d disabled:opacity-40 disabled:cursor-not-allowed"
            >
                {isSubmitting ? "Проверяем..." : "Проверить"}
            </button>
        </div>
    );
}
