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
            {content.situation && (
                <div className="bg-[#F7F7F7] rounded-2xl p-4">
                    <p className="text-sm text-gray-500 mb-1">Ситуация</p>
                    <p className="text-gray-900 font-medium">{content.situation}</p>
                </div>
            )}

            <p className="font-[var(--font-space-grotesk)] text-xl font-bold text-gray-900">
                {content.question}
            </p>

            <div className="flex flex-col gap-3">
                {content.options.map((optionText, optionIndex) => (
                    <button
                        key={optionIndex}
                        onClick={() => setSelectedOptionIndex(optionIndex)}
                        className={`px-5 py-4 rounded-2xl text-left font-medium transition-colors border-2 ${
                            selectedOptionIndex === optionIndex
                                ? "border-[#58CC02] bg-[#E8F9D6] text-gray-900"
                                : "border-transparent bg-[#F7F7F7] text-gray-700 hover:bg-[#E8F9D6]"
                        }`}
                    >
                        {optionText}
                    </button>
                ))}
            </div>

            <button
                onClick={() => {
                    if (selectedOptionIndex !== null)
                        onSubmit({ selectedOptionIndex });
                }}
                disabled={selectedOptionIndex === null || isSubmitting}
                className="py-4 rounded-2xl bg-[#58CC02] text-white font-bold shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-40"
            >
                {isSubmitting ? "Проверяем..." : "Проверить"}
            </button>
        </div>
    );
}
