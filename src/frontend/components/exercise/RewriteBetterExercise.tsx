"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface RewriteBetterContent {
    situation: string;
    originalText: string;
    context?: string;
    aiPrompt: string;
    minLength?: number;
    maxLength?: number;
}

interface RewriteBetterExerciseProps {
    content: RewriteBetterContent;
    onSubmit: (answer: { rewrittenText: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function RewriteBetterExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: RewriteBetterExerciseProps) {
    const [rewrittenText, setRewrittenText] = useState("");

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const minLength = content.minLength ?? 10;
    const maxLength = content.maxLength ?? 500;
    const charCount = rewrittenText.length;
    const isValidLength = charCount >= minLength && charCount <= maxLength;

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
                        <Icon name="edit-3" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            {content.context && (
                <div className="p-4 rounded-xl bg-surface-container-low border border-outline-variant">
                    <p className="text-sm text-on-surface-variant">
                        <span className="font-medium">Контекст:</span> {content.context}
                    </p>
                </div>
            )}

            <div className="p-4 rounded-xl bg-surface-container border-2 border-outline-variant">
                <p className="text-xs font-medium text-on-surface-variant mb-2">Оригинал:</p>
                <p className="text-on-surface italic">{content.originalText}</p>
            </div>

            <div className="flex flex-col gap-2">
                <label className="font-medium text-sm text-on-surface">
                    Ваша улучшенная версия:
                </label>
                <textarea
                    value={rewrittenText}
                    onChange={(e) => setRewrittenText(e.target.value)}
                    disabled={isAnswered}
                    placeholder="Напишите улучшенную версию..."
                    className="w-full p-4 rounded-xl bg-surface-container border-2 border-outline-variant focus:border-primary outline-none resize-none min-h-[120px] disabled:opacity-60"
                />
                <div className="flex justify-between text-xs">
                    <span className={charCount < minLength ? "text-error" : "text-on-surface-variant"}>
                        Минимум {minLength} символов
                    </span>
                    <span className={charCount > maxLength ? "text-error" : "text-on-surface-variant"}>
                        {charCount} / {maxLength}
                    </span>
                </div>
            </div>

            {isAnswered && (
                <div className="flex flex-col gap-3">
                    <div className="flex items-center gap-3">
                        <span className={`px-3 py-1 rounded-full text-sm font-bold ${getRatingColor(submittedResult.score)}`}>
                            {Math.round(submittedResult.score / 10)}/10
                        </span>
                        <span className={`font-medium ${submittedResult.isCorrect ? "text-primary" : "text-error"}`}>
                            {submittedResult.isCorrect ? "Отличное улучшение!" : "Есть куда расти"}
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
                        onClick={() => onSubmit({ rewrittenText })}
                        disabled={!isValidLength || isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Проверяем..." : "Проверить"}
                    </button>
                )}
            </div>
        </div>
    );
}
