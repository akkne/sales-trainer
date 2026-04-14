"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface DialogueLine {
    speaker: string;
    text: string;
    is_mistake: boolean;
}

interface SpotMistakeContent {
    dialogue: DialogueLine[];
    explanation?: string;
    ai_prompt?: string;
}

interface SpotMistakeExerciseProps {
    content: SpotMistakeContent;
    onSubmit: (answer: { selectedLineIndex: number; explanation?: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function SpotMistakeExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: SpotMistakeExerciseProps) {
    const [selectedLineIndex, setSelectedLineIndex] = useState<number | null>(null);
    const [explanation, setExplanation] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const mistakeIndex = content.dialogue.findIndex(line => line.is_mistake);

    function lineStyle(lineIndex: number): string {
        const base = "flex gap-3 px-4 py-3 rounded-xl transition-colors cursor-pointer";

        if (!isAnswered) {
            if (selectedLineIndex === lineIndex) {
                return `${base} bg-error-container border-2 border-error`;
            }
            return `${base} bg-surface-container hover:bg-surface-container-high`;
        }

        const isMistakeLine = lineIndex === mistakeIndex;
        const wasSelected = selectedLineIndex === lineIndex;

        if (wasSelected && isMistakeLine) {
            return `${base} bg-primary-container border-2 border-primary`;
        }
        if (wasSelected && !isMistakeLine) {
            return `${base} bg-error-container border-2 border-error`;
        }
        if (isMistakeLine) {
            return `${base} bg-primary-container/50 border-2 border-primary/50`;
        }
        return `${base} bg-surface-container`;
    }

    const canSubmit = selectedLineIndex !== null;

    function handleSubmit() {
        if (selectedLineIndex === null) return;
        onSubmit({
            selectedLineIndex,
            explanation: explanation.length > 10 ? explanation : undefined,
        });
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-full bg-error-container flex items-center justify-center shrink-0 mt-1">
                    <Icon name="search" size="sm" className="text-error" />
                </div>
                <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                    <p className="text-sm text-on-surface-variant">Найдите ошибку в диалоге</p>
                </div>
            </div>

            <p className="font-headline font-bold text-lg text-on-surface">
                Нажмите на строку с ошибкой:
            </p>

            <div className="flex flex-col gap-2">
                {content.dialogue.map((line, index) => (
                    <button
                        key={index}
                        onClick={() => !isAnswered && setSelectedLineIndex(index)}
                        disabled={isAnswered}
                        className={lineStyle(index)}
                    >
                        <span className="font-bold text-sm shrink-0 w-20 capitalize">{line.speaker}:</span>
                        <span className="text-left">{line.text}</span>
                    </button>
                ))}
            </div>

            {/* Optional explanation textarea */}
            {selectedLineIndex !== null && !isAnswered && (
                <div className="flex flex-col gap-2">
                    <label className="font-medium text-sm text-on-surface">
                        Объясните, почему это ошибка (опционально):
                    </label>
                    <textarea
                        value={explanation}
                        onChange={(e) => setExplanation(e.target.value)}
                        placeholder="Напишите ваше объяснение..."
                        className="w-full p-4 rounded-xl bg-surface-container border-2 border-outline-variant focus:border-primary outline-none resize-none min-h-[100px]"
                    />
                </div>
            )}

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
                        onClick={handleSubmit}
                        disabled={!canSubmit || isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Проверяем..." : "Проверить"}
                    </button>
                )}
            </div>
        </div>
    );
}
