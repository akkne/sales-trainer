"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface MatchingItem {
    id: string;
    text: string;
}

interface MatchingPair {
    left: string;
    right: string;
}

interface MatchingContent {
    situation: string;
    leftColumn: MatchingItem[];
    rightColumn: MatchingItem[];
    correctPairs: MatchingPair[];
    explanation?: string;
}

interface MatchingExerciseProps {
    content: MatchingContent;
    onSubmit: (answer: { pairs: MatchingPair[] }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function MatchingExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: MatchingExerciseProps) {
    const [selectedLeft, setSelectedLeft] = useState<string | null>(null);
    const [pairs, setPairs] = useState<MatchingPair[]>([]);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    const connectedLeftIds = useMemo(() => new Set(pairs.map(p => p.left)), [pairs]);
    const connectedRightIds = useMemo(() => new Set(pairs.map(p => p.right)), [pairs]);

    function handleLeftClick(id: string) {
        if (isAnswered || connectedLeftIds.has(id)) return;
        setSelectedLeft(id === selectedLeft ? null : id);
    }

    function handleRightClick(id: string) {
        if (isAnswered || connectedRightIds.has(id) || !selectedLeft) return;
        setPairs([...pairs, { left: selectedLeft, right: id }]);
        setSelectedLeft(null);
    }

    function removePair(leftId: string) {
        if (isAnswered) return;
        setPairs(pairs.filter(p => p.left !== leftId));
    }

    function resetAll() {
        setPairs([]);
        setSelectedLeft(null);
    }

    const correctPairsSet = useMemo(() => {
        return new Set(content.correctPairs.map(p => `${p.left}:${p.right}`));
    }, [content.correctPairs]);

    function leftItemStyle(id: string): string {
        const base = "px-4 py-3 rounded-xl font-medium transition-colors border-2 text-left";

        if (!isAnswered) {
            if (connectedLeftIds.has(id)) {
                return `${base} border-tertiary bg-tertiary-container text-tertiary`;
            }
            if (selectedLeft === id) {
                return `${base} border-primary bg-primary-container text-primary`;
            }
            return `${base} border-outline-variant bg-surface-container text-on-surface hover:border-outline`;
        }

        const pair = pairs.find(p => p.left === id);
        if (pair && correctPairsSet.has(`${pair.left}:${pair.right}`)) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        return `${base} border-error bg-error-container text-error`;
    }

    function rightItemStyle(id: string): string {
        const base = "px-4 py-3 rounded-xl font-medium transition-colors border-2 text-left";

        if (!isAnswered) {
            if (connectedRightIds.has(id)) {
                return `${base} border-tertiary bg-tertiary-container text-tertiary`;
            }
            if (selectedLeft && !connectedRightIds.has(id)) {
                return `${base} border-outline-variant bg-surface-container text-on-surface hover:border-primary cursor-pointer`;
            }
            return `${base} border-outline-variant bg-surface-container text-on-surface`;
        }

        const pair = pairs.find(p => p.right === id);
        if (pair && correctPairsSet.has(`${pair.left}:${pair.right}`)) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        if (pair) {
            return `${base} border-error bg-error-container text-error`;
        }
        return `${base} border-outline-variant bg-surface-container text-on-surface-variant`;
    }

    const canSubmit = pairs.length === content.leftColumn.length;

    return (
        <div className="flex flex-col gap-6">
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="link" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Соедините пары:
            </p>

            <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col gap-2">
                    {content.leftColumn.map((item) => (
                        <button
                            key={item.id}
                            onClick={() => handleLeftClick(item.id)}
                            disabled={isAnswered || connectedLeftIds.has(item.id)}
                            className={leftItemStyle(item.id)}
                        >
                            {item.text}
                            {!isAnswered && connectedLeftIds.has(item.id) && (
                                <button
                                    type="button"
                                    onClick={(e) => { e.stopPropagation(); removePair(item.id); }}
                                    className="ml-2 text-xs text-tertiary hover:text-error"
                                >
                                    ✕
                                </button>
                            )}
                        </button>
                    ))}
                </div>

                <div className="flex flex-col gap-2">
                    {content.rightColumn.map((item) => (
                        <button
                            key={item.id}
                            onClick={() => handleRightClick(item.id)}
                            disabled={isAnswered || connectedRightIds.has(item.id) || !selectedLeft}
                            className={rightItemStyle(item.id)}
                        >
                            {item.text}
                        </button>
                    ))}
                </div>
            </div>

            {!isAnswered && pairs.length > 0 && (
                <button
                    type="button"
                    onClick={resetAll}
                    className="text-sm text-on-surface-variant hover:text-error transition-colors"
                >
                    Сбросить все связи
                </button>
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
                        onClick={() => onSubmit({ pairs })}
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
