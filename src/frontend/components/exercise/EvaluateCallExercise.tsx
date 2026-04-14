"use client";

import { useState } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface TranscriptLine {
    speaker: string;
    text: string;
}

interface EvaluationAxis {
    name: string;
    description: string;
}

interface EvaluateCallContent {
    transcript: TranscriptLine[];
    evaluation_axes: EvaluationAxis[];
    ai_prompt?: string;
}

interface EvaluateCallExerciseProps {
    content: EvaluateCallContent;
    onSubmit: (answer: { ratings: Record<string, number>; overallComment?: string }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function EvaluateCallExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: EvaluateCallExerciseProps) {
    const [ratings, setRatings] = useState<Record<string, number>>({});
    const [overallComment, setOverallComment] = useState("");
    const [showTranscript, setShowTranscript] = useState(true);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;
    const allRated = content.evaluation_axes.every(axis => ratings[axis.name] !== undefined);

    function setRating(axisName: string, value: number) {
        setRatings({ ...ratings, [axisName]: value });
    }

    function getRatingColor(score: number): string {
        if (score >= 80) return "bg-primary text-on-primary";
        if (score >= 60) return "bg-tertiary text-on-tertiary";
        return "bg-error text-on-error";
    }

    const ratingOptions = [1, 2, 3, 4, 5];

    return (
        <div className="flex flex-col gap-6">
            <div className="flex items-start gap-3">
                <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                    <Icon name="phone" size="sm" className="text-primary" />
                </div>
                <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                    <p className="text-sm text-on-surface-variant">Оцените звонок по критериям</p>
                </div>
            </div>

            {/* Transcript toggle */}
            <div className="flex flex-col gap-2">
                <button
                    type="button"
                    onClick={() => setShowTranscript(!showTranscript)}
                    className="flex items-center gap-2 text-sm font-medium text-on-surface hover:text-primary transition-colors"
                >
                    <Icon name={showTranscript ? "chevron-down" : "chevron-right"} size="sm" />
                    Транскрипт звонка
                </button>

                {showTranscript && (
                    <div className="flex flex-col gap-1 p-4 bg-surface-container rounded-xl max-h-[250px] overflow-y-auto">
                        {content.transcript.map((line, idx) => (
                            <div key={idx} className="flex gap-2">
                                <span className="font-bold text-sm shrink-0 w-20 text-on-surface-variant capitalize">
                                    {line.speaker}:
                                </span>
                                <span className="text-sm text-on-surface">{line.text}</span>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* Evaluation axes ratings */}
            <div className="flex flex-col gap-4">
                <p className="font-headline font-bold text-lg text-on-surface">
                    Оцените звонок по критериям:
                </p>

                {content.evaluation_axes.map((axis) => (
                    <div key={axis.name} className="flex flex-col gap-2 p-4 bg-surface-container rounded-xl">
                        <div className="flex justify-between items-start">
                            <div>
                                <p className="font-medium text-on-surface">{axis.name}</p>
                                <p className="text-xs text-on-surface-variant">{axis.description}</p>
                            </div>
                        </div>
                        <div className="flex gap-2 mt-2">
                            {ratingOptions.map((value) => (
                                <button
                                    key={value}
                                    onClick={() => !isAnswered && setRating(axis.name, value)}
                                    disabled={isAnswered}
                                    className={`w-10 h-10 rounded-lg font-bold transition-colors ${
                                        ratings[axis.name] === value
                                            ? "bg-primary text-on-primary"
                                            : "bg-surface-container-high text-on-surface hover:bg-surface-container-highest"
                                    } disabled:opacity-60`}
                                >
                                    {value}
                                </button>
                            ))}
                        </div>
                    </div>
                ))}
            </div>

            {/* Overall comment */}
            {!isAnswered && (
                <div className="flex flex-col gap-2">
                    <label className="font-medium text-sm text-on-surface">
                        Общий комментарий (опционально):
                    </label>
                    <textarea
                        value={overallComment}
                        onChange={(e) => setOverallComment(e.target.value)}
                        placeholder="Ваши наблюдения о звонке..."
                        className="w-full p-4 rounded-xl bg-surface-container border-2 border-outline-variant focus:border-primary outline-none resize-none min-h-[80px]"
                    />
                </div>
            )}

            {isAnswered && (
                <div className="flex flex-col gap-3 p-4 bg-surface-container rounded-xl">
                    <div className="flex items-center gap-3">
                        <span className={`px-3 py-1 rounded-full text-sm font-bold ${getRatingColor(submittedResult.score)}`}>
                            {Math.round(submittedResult.score / 10)}/10
                        </span>
                        <span className={`font-medium ${submittedResult.isCorrect ? "text-primary" : "text-error"}`}>
                            {submittedResult.isCorrect ? "Отличный анализ!" : "Можно точнее"}
                        </span>
                    </div>
                    {submittedResult.aiFeedback && (
                        <div className="text-sm text-on-surface-variant leading-relaxed whitespace-pre-wrap">
                            {submittedResult.aiFeedback}
                        </div>
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
                        onClick={() => onSubmit({ ratings, overallComment: overallComment || undefined })}
                        disabled={!allRated || isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Анализируем..." : "Отправить оценку"}
                    </button>
                )}
            </div>
        </div>
    );
}
