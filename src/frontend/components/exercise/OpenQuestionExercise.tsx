"use client";

import { useState } from "react";
import { type ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { useKeyboardControls } from "@/lib/hooks/useKeyboardControls";

interface OpenQuestionContent {
    question: string;
    evaluationCriteria?: string;
}

interface OpenQuestionExerciseProps {
    content: OpenQuestionContent;
    onSubmit: (answer: { text: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function OpenQuestionExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: OpenQuestionExerciseProps) {
    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const [responseText, setResponseText] = useState("");

    const isBusy = isSubmitting || isAnswered;

    // Extract rating from explanation (format: "Оценка: X/10")
    const rating = submittedResult?.explanation
        ? parseInt(submittedResult.explanation.match(/(\d+)\/10/)?.[1] ?? "0", 10)
        : null;

    useKeyboardControls({
        optionCount: 0,
        onSelectOption: () => {},
        onSubmit: () => {
            if (responseText.trim() && !isBusy) {
                onSubmit({ text: responseText.trim() });
            }
        },
        onContinue: () => onContinue?.(),
        isAnswered,
        disabled: isBusy,
        inputFocused: true,
    });

    return (
        <div className="flex flex-col gap-6">
            {/* Question */}
            <p className="font-extrabold text-xl text-gray-900">{content.question}</p>

            <textarea
                value={responseText}
                onChange={(e) => setResponseText(e.target.value)}
                placeholder="Напиши свой ответ..."
                rows={5}
                disabled={isAnswered}
                className="w-full px-4 py-3 rounded-2xl bg-[#F7F7F7] text-gray-900 placeholder-gray-400 outline-none focus:ring-2 focus:ring-[#58CC02] resize-none disabled:opacity-60"
            />

            {/* Rating and improvements display */}
            {isAnswered && rating !== null && (
                <div className="flex flex-col gap-3">
                    {/* Rating badge */}
                    <div className="flex items-center gap-3">
                        <div
                            className={`text-3xl font-extrabold px-4 py-2 rounded-xl ${
                                rating >= 8
                                    ? "bg-[#D7FFB8] text-[#58CC02]"
                                    : rating >= 5
                                    ? "bg-[#FFF4CC] text-[#FFC800]"
                                    : "bg-[#FFDEDE] text-[#FF4B4B]"
                            }`}
                        >
                            {rating}/10
                        </div>
                        <span
                            className={`text-sm font-semibold ${
                                rating >= 8 ? "text-[#58CC02]" : "text-[#AFAFAF]"
                            }`}
                        >
                            {rating >= 8 ? "Отлично!" : rating >= 5 ? "Неплохо" : "Нужно доработать"}
                        </span>
                    </div>

                    {/* Improvements feedback */}
                    {submittedResult.aiFeedback && (
                        <div className="bg-[#F7F7F7] rounded-2xl px-4 py-3">
                            <p className="text-xs text-[#AFAFAF] uppercase tracking-wider mb-1">
                                Что улучшить
                            </p>
                            <p className="text-sm text-gray-700 leading-relaxed">
                                {submittedResult.aiFeedback}
                            </p>
                        </div>
                    )}
                </div>
            )}

            <div className="flex gap-3">
                {!isAnswered && onSkip && (
                    <button
                        onClick={onSkip}
                        disabled={isBusy}
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
                            if (responseText.trim()) onSubmit({ text: responseText.trim() });
                        }}
                        disabled={!responseText.trim() || isBusy}
                        className="flex-1 py-4 rounded-2xl bg-[#58CC02] text-white font-extrabold btn-3d disabled:opacity-40 disabled:cursor-not-allowed"
                    >
                        {isSubmitting ? "AI оценивает..." : "Отправить"}
                    </button>
                )}
            </div>
        </div>
    );
}
