"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface MatchPair {
    left: string;
    right: string;
}

interface MatchPairsContent {
    instruction: string;
    pairs: MatchPair[];
    explanation?: string;
}

interface MatchPairsExerciseProps {
    content: MatchPairsContent;
    onSubmit: (answer: { pairs: MatchPair[] }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function MatchPairsExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: MatchPairsExerciseProps) {
    // Extract unique left and right items from pairs
    const leftItems = useMemo(() => content.pairs.map(p => p.left), [content.pairs]);
    const rightItems = useMemo(() => {
        // Shuffle right items
        const items = content.pairs.map(p => p.right);
        return items.sort(() => Math.random() - 0.5);
    }, [content.pairs]);

    const [selectedLeft, setSelectedLeft] = useState<string | null>(null);
    const [userPairs, setUserPairs] = useState<MatchPair[]>([]);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    const connectedLefts = useMemo(() => new Set(userPairs.map(p => p.left)), [userPairs]);
    const connectedRights = useMemo(() => new Set(userPairs.map(p => p.right)), [userPairs]);

    function handleLeftClick(item: string) {
        if (isAnswered || connectedLefts.has(item)) return;
        setSelectedLeft(item === selectedLeft ? null : item);
    }

    function handleRightClick(item: string) {
        if (isAnswered || connectedRights.has(item) || !selectedLeft) return;
        setUserPairs([...userPairs, { left: selectedLeft, right: item }]);
        setSelectedLeft(null);
    }

    function removePair(leftItem: string) {
        if (isAnswered) return;
        setUserPairs(userPairs.filter(p => p.left !== leftItem));
    }

    function resetAll() {
        setUserPairs([]);
        setSelectedLeft(null);
    }

    const correctPairsSet = useMemo(() => {
        return new Set(content.pairs.map(p => `${p.left}:${p.right}`));
    }, [content.pairs]);

    function leftItemStyle(item: string): string {
        const base = "px-4 py-3 rounded-xl font-medium transition-colors border-2 text-left";

        if (!isAnswered) {
            if (connectedLefts.has(item)) {
                return `${base} border-tertiary bg-tertiary-container text-tertiary`;
            }
            if (selectedLeft === item) {
                return `${base} border-primary bg-primary-container text-primary`;
            }
            return `${base} border-outline-variant bg-surface-container text-on-surface hover:border-outline`;
        }

        const pair = userPairs.find(p => p.left === item);
        if (pair && correctPairsSet.has(`${pair.left}:${pair.right}`)) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        return `${base} border-error bg-error-container text-error`;
    }

    function rightItemStyle(item: string): string {
        const base = "px-4 py-3 rounded-xl font-medium transition-colors border-2 text-left";

        if (!isAnswered) {
            if (connectedRights.has(item)) {
                return `${base} border-tertiary bg-tertiary-container text-tertiary`;
            }
            if (selectedLeft && !connectedRights.has(item)) {
                return `${base} border-outline-variant bg-surface-container text-on-surface hover:border-primary cursor-pointer`;
            }
            return `${base} border-outline-variant bg-surface-container text-on-surface`;
        }

        const pair = userPairs.find(p => p.right === item);
        if (pair && correctPairsSet.has(`${pair.left}:${pair.right}`)) {
            return `${base} border-primary bg-primary-container text-primary`;
        }
        if (pair) {
            return `${base} border-error bg-error-container text-error`;
        }
        return `${base} border-outline-variant bg-surface-container text-on-surface-variant`;
    }

    const canSubmit = userPairs.length === leftItems.length;

    return (
        <div className="flex flex-col gap-6">
            {content.instruction && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="link" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.instruction}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Соедините пары:
            </p>

            <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col gap-2">
                    {leftItems.map((item) => (
                        <button
                            key={item}
                            onClick={() => handleLeftClick(item)}
                            disabled={isAnswered || connectedLefts.has(item)}
                            className={leftItemStyle(item)}
                        >
                            {item}
                            {!isAnswered && connectedLefts.has(item) && (
                                <button
                                    type="button"
                                    onClick={(e) => { e.stopPropagation(); removePair(item); }}
                                    className="ml-2 text-xs text-tertiary hover:text-error"
                                >
                                    ✕
                                </button>
                            )}
                        </button>
                    ))}
                </div>

                <div className="flex flex-col gap-2">
                    {rightItems.map((item) => (
                        <button
                            key={item}
                            onClick={() => handleRightClick(item)}
                            disabled={isAnswered || connectedRights.has(item) || !selectedLeft}
                            className={rightItemStyle(item)}
                        >
                            {item}
                        </button>
                    ))}
                </div>
            </div>

            {!isAnswered && userPairs.length > 0 && (
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
                        onClick={() => onSubmit({ pairs: userPairs })}
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
