"use client";

import { useState } from "react";

interface FillBlankContent {
    characterName: string;
    characterLine: string;
    options: string[];
    correctOptionIndex: number;
    explanation?: string;
}

interface FillBlankExerciseProps {
    content: FillBlankContent;
    onSubmit: (answer: { selectedOptionIndex: number }) => void;
    isSubmitting: boolean;
}

export function FillBlankExercise({
    content,
    onSubmit,
    isSubmitting,
}: FillBlankExerciseProps) {
    const [selectedOptionIndex, setSelectedOptionIndex] = useState<number | null>(null);

    return (
        <div className="flex flex-col gap-6">
            <div className="bg-[#F7F7F7] rounded-2xl p-5">
                <div className="flex items-center gap-2 mb-3">
                    <div className="w-8 h-8 rounded-full bg-gray-300 flex items-center justify-center text-sm font-bold">
                        {content.characterName[0]}
                    </div>
                    <span className="font-semibold text-gray-600 text-sm">
                        {content.characterName}
                    </span>
                </div>
                <p className="text-gray-900 font-medium italic">
                    &ldquo;{content.characterLine}&rdquo;
                </p>
            </div>

            <p className="font-[var(--font-space-grotesk)] text-lg font-bold text-gray-900">
                Выбери лучший ответ:
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
