"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface FreeTextContent {
    situation?: string;
    instruction: string;
    evaluation_criteria?: string[];
    ai_prompt?: string;
}

interface FreeTextExerciseProps {
    content: FreeTextContent;
    onSubmit: (answer: { text: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function FreeTextExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: FreeTextExerciseProps) {
    const [text, setText] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const minLength = 20;
    const charCount = text.length;
    const isValidLength = charCount >= minLength;

    function getRatingColor(score: number): string {
        if (score >= 80) return "bg-primary text-on-primary";
        if (score >= 60) return "bg-tertiary text-on-tertiary";
        return "bg-error text-on-error";
    }

    return (
        <div className="flex flex-col gap-6">
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="message-square" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-on-surface">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                {content.instruction}
            </p>

            {content.evaluation_criteria && content.evaluation_criteria.length > 0 && (
                <div className="p-4 rounded-xl bg-surface-container-low border border-outline-variant">
                    <p className="text-xs font-medium text-on-surface-variant mb-2">Критерии оценки:</p>
                    <ul className="text-sm text-on-surface-variant list-disc list-inside space-y-1">
                        {content.evaluation_criteria.map((criterion, idx) => (
                            <li key={idx}>{criterion}</li>
                        ))}
                    </ul>
                </div>
            )}

            <div className="flex flex-col gap-2">
                <textarea
                    value={text}
                    onChange={(e) => setText(e.target.value)}
                    disabled={isAnswered}
                    placeholder="Напишите ваш ответ..."
                    className="w-full p-4 rounded-xl bg-surface-container border-2 border-outline-variant focus:border-primary outline-none resize-none min-h-[150px] disabled:opacity-60"
                />
                <div className="flex justify-between text-xs">
                    <span className={charCount < minLength ? "text-error" : "text-on-surface-variant"}>
                        Минимум {minLength} символов
                    </span>
                    <span className="text-on-surface-variant">
                        {charCount} символов
                    </span>
                </div>
            </div>

            {isAnswered && (
                <div className="flex flex-col gap-3 p-4 bg-surface-container rounded-xl">
                    <div className="flex items-center gap-3">
                        <span className={`px-3 py-1 rounded-full text-sm font-bold ${getRatingColor(submittedResult.score)}`}>
                            {Math.round(submittedResult.score / 10)}/10
                        </span>
                        <span className={`font-medium ${submittedResult.isCorrect ? "text-primary" : "text-error"}`}>
                            {submittedResult.isCorrect ? "Отличный ответ!" : "Есть что улучшить"}
                        </span>
                    </div>
                    {submittedResult.aiFeedback && (
                        <p className="text-sm text-on-surface-variant leading-relaxed">
                            {submittedResult.aiFeedback}
                        </p>
                    )}
                </div>
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
                        onClick={() => onSubmit({ text })}
                        disabled={!isValidLength || isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Проверяем..." : "Отправить"}
                    </button>
                )}
            </div>
        </div>
    );
}
