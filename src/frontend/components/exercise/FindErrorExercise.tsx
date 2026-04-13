"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface DialogLine {
    id: string;
    speaker: string;
    text: string;
}

interface SuggestedFix {
    id: string;
    text: string;
}

interface FindErrorContent {
    situation: string;
    dialogLines: DialogLine[];
    errorLineId: string;
    aiPrompt?: string;
    requireExplanation?: boolean;
    suggestedFixes?: SuggestedFix[];
    correctFixIds?: string[];
}

interface FindErrorExerciseProps {
    content: FindErrorContent;
    onSubmit: (answer: { selectedLineId: string; explanation?: string; selectedFixId?: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function FindErrorExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: FindErrorExerciseProps) {
    const [selectedLineId, setSelectedLineId] = useState<string | null>(null);
    const [explanation, setExplanation] = useState("");
    const [selectedFixId, setSelectedFixId] = useState<string | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const showExplanation = content.requireExplanation && selectedLineId;
    const showFixes = content.suggestedFixes && content.suggestedFixes.length > 0 && (showExplanation ? explanation.length > 10 : selectedLineId);

    function lineStyle(lineId: string): string {
        const base = "flex gap-3 px-4 py-3 rounded-xl transition-colors cursor-pointer";

        if (!isAnswered) {
            if (selectedLineId === lineId) {
                return `${base} bg-error-container border-2 border-error`;
            }
            return `${base} bg-surface-container hover:bg-surface-container-high`;
        }

        const isErrorLine = lineId === content.errorLineId;
        const wasSelected = selectedLineId === lineId;

        if (wasSelected && isErrorLine) {
            return `${base} bg-primary-container border-2 border-primary`;
        }
        if (wasSelected && !isErrorLine) {
            return `${base} bg-error-container border-2 border-error`;
        }
        if (isErrorLine) {
            return `${base} bg-primary-container/50 border-2 border-primary/50`;
        }
        return `${base} bg-surface-container`;
    }

    function fixStyle(fixId: string): string {
        const base = "px-4 py-3 rounded-xl font-medium transition-colors border-2 text-left";

        if (!isAnswered) {
            if (selectedFixId === fixId) {
                return `${base} border-primary bg-primary-container text-primary`;
            }
            return `${base} border-outline-variant bg-surface-container text-on-surface hover:border-outline`;
        }

        const isCorrectFix = content.correctFixIds?.includes(fixId);

        if (selectedFixId === fixId && isCorrectFix) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        if (selectedFixId === fixId && !isCorrectFix) {
            return `${base} border-error bg-error-container text-error`;
        }
        if (isCorrectFix) {
            return `${base} border-primary/50 bg-primary-container/50 text-primary`;
        }
        return `${base} border-outline-variant bg-surface-container text-on-surface-variant`;
    }

    const canSubmit = selectedLineId && (!content.requireExplanation || explanation.length > 10) && (!content.suggestedFixes || selectedFixId);

    function handleSubmit() {
        if (!selectedLineId) return;
        onSubmit({
            selectedLineId,
            explanation: content.requireExplanation ? explanation : undefined,
            selectedFixId: selectedFixId || undefined,
        });
    }

    return (
        <div className="flex flex-col gap-6">
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-error-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="search" size="sm" className="text-error" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Найдите строку с ошибкой:
            </p>

            <div className="flex flex-col gap-2">
                {content.dialogLines.map((line) => (
                    <button
                        key={line.id}
                        onClick={() => !isAnswered && setSelectedLineId(line.id)}
                        disabled={isAnswered}
                        className={lineStyle(line.id)}
                    >
                        <span className="font-bold text-sm shrink-0 w-24">{line.speaker}:</span>
                        <span className="text-left">{line.text}</span>
                    </button>
                ))}
            </div>

            {/* Explanation textarea */}
            {showExplanation && !isAnswered && (
                <div className="flex flex-col gap-2">
                    <label className="font-medium text-sm text-on-surface">
                        Объясните, почему это ошибка:
                    </label>
                    <textarea
                        value={explanation}
                        onChange={(e) => setExplanation(e.target.value)}
                        placeholder="Напишите ваше объяснение..."
                        className="w-full p-4 rounded-xl bg-surface-container border-2 border-outline-variant focus:border-primary outline-none resize-none min-h-[100px]"
                    />
                </div>
            )}

            {/* Fix selection */}
            {showFixes && (
                <div className="flex flex-col gap-2">
                    <label className="font-medium text-sm text-on-surface">
                        Выберите лучшее исправление:
                    </label>
                    <div className="flex flex-col gap-2">
                        {content.suggestedFixes!.map((fix) => (
                            <button
                                key={fix.id}
                                onClick={() => !isAnswered && setSelectedFixId(fix.id)}
                                disabled={isAnswered}
                                className={fixStyle(fix.id)}
                            >
                                {fix.text}
                            </button>
                        ))}
                    </div>
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
