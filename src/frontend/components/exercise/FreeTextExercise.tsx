"use client";

import { useState } from "react";

interface FreeTextContent {
    situation: string;
    prompt: string;
    evaluationCriteria: string;
}

interface FreeTextExerciseProps {
    content: FreeTextContent;
    onSubmit: (answer: { text: string }) => void;
    isSubmitting: boolean;
}

export function FreeTextExercise({
    content,
    onSubmit,
    isSubmitting,
}: FreeTextExerciseProps) {
    const [responseText, setResponseText] = useState("");

    return (
        <div className="flex flex-col gap-6">
            <div className="bg-[#F7F7F7] rounded-2xl p-4">
                <p className="text-sm text-gray-500 mb-1">Ситуация</p>
                <p className="text-gray-900 font-medium">{content.situation}</p>
            </div>

            <p className="font-[var(--font-space-grotesk)] text-xl font-bold text-gray-900">
                {content.prompt}
            </p>

            <textarea
                value={responseText}
                onChange={(event) => setResponseText(event.target.value)}
                placeholder="Напиши свой ответ..."
                rows={5}
                className="px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02] resize-none"
            />

            <button
                onClick={() => {
                    if (responseText.trim()) onSubmit({ text: responseText.trim() });
                }}
                disabled={!responseText.trim() || isSubmitting}
                className="py-4 rounded-2xl bg-[#58CC02] text-white font-bold shadow-[0_4px_0_#4CAD00] active:shadow-none active:translate-y-1 transition-transform disabled:opacity-40"
            >
                {isSubmitting ? "AI оценивает..." : "Отправить"}
            </button>
        </div>
    );
}
