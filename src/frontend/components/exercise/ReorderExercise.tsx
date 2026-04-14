"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface ReorderItem {
    text: string;
    correct_position: number;
}

interface ReorderContent {
    instruction: string;
    items: ReorderItem[];
    explanation?: string;
}

interface ReorderExerciseProps {
    content: ReorderContent;
    onSubmit: (answer: { order: number[] }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function ReorderExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: ReorderExerciseProps) {
    // Shuffle items on first render
    const shuffledIndices = useMemo(() => {
        const indices = content.items.map((_, i) => i);
        // Simple shuffle
        for (let i = indices.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [indices[i], indices[j]] = [indices[j], indices[i]];
        }
        return indices;
    }, [content.items]);

    const [orderedIndices, setOrderedIndices] = useState<number[]>(shuffledIndices);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    // Build correct order from correct_position
    const correctOrder = useMemo(() => {
        return content.items
            .map((item, idx) => ({ idx, pos: item.correct_position }))
            .sort((a, b) => a.pos - b.pos)
            .map(x => x.idx);
    }, [content.items]);

    function handleDragStart(index: number) {
        if (isAnswered) return;
        setDraggedIndex(index);
    }

    function handleDragOver(e: React.DragEvent, targetIndex: number) {
        e.preventDefault();
        if (draggedIndex === null || draggedIndex === targetIndex || isAnswered) return;

        const newOrder = [...orderedIndices];
        const [dragged] = newOrder.splice(draggedIndex, 1);
        newOrder.splice(targetIndex, 0, dragged);
        setOrderedIndices(newOrder);
        setDraggedIndex(targetIndex);
    }

    function handleDragEnd() {
        setDraggedIndex(null);
    }

    function moveItem(fromIndex: number, direction: "up" | "down") {
        if (isAnswered) return;
        const toIndex = direction === "up" ? fromIndex - 1 : fromIndex + 1;
        if (toIndex < 0 || toIndex >= orderedIndices.length) return;

        const newOrder = [...orderedIndices];
        [newOrder[fromIndex], newOrder[toIndex]] = [newOrder[toIndex], newOrder[fromIndex]];
        setOrderedIndices(newOrder);
    }

    function itemStyle(itemIdx: number, position: number): string {
        const base = "flex items-center gap-3 px-4 py-3 rounded-xl text-left font-medium transition-colors border-b-4 cursor-grab active:cursor-grabbing";

        if (!isAnswered) {
            return `${base} border-outline-variant bg-surface-container text-on-surface hover:bg-surface-container-high`;
        }

        const correctPosition = correctOrder.indexOf(itemIdx);
        const isCorrectPosition = position === correctPosition;

        if (isCorrectPosition) {
            return `${base} border-primary bg-primary-container text-primary cursor-default`;
        }
        return `${base} border-error bg-error-container text-error cursor-default`;
    }

    return (
        <div className="flex flex-col gap-6">
            {content.instruction && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="list-ordered" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.instruction}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Расставьте элементы в правильном порядке:
            </p>

            <div className="flex flex-col gap-2">
                {orderedIndices.map((itemIdx, position) => {
                    const item = content.items[itemIdx];
                    if (!item) return null;

                    return (
                        <div
                            key={itemIdx}
                            draggable={!isAnswered}
                            onDragStart={() => handleDragStart(position)}
                            onDragOver={(e) => handleDragOver(e, position)}
                            onDragEnd={handleDragEnd}
                            className={itemStyle(itemIdx, position)}
                        >
                            <span className="w-7 h-7 rounded-full bg-surface-container-highest flex items-center justify-center text-sm font-bold shrink-0">
                                {position + 1}
                            </span>
                            <span className="flex-1">{item.text}</span>
                            {!isAnswered && (
                                <div className="flex flex-col gap-1">
                                    <button
                                        type="button"
                                        onClick={() => moveItem(position, "up")}
                                        disabled={position === 0}
                                        className="p-1 rounded hover:bg-surface-container-highest disabled:opacity-30"
                                    >
                                        <Icon name="chevron-up" size="sm" />
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => moveItem(position, "down")}
                                        disabled={position === orderedIndices.length - 1}
                                        className="p-1 rounded hover:bg-surface-container-highest disabled:opacity-30"
                                    >
                                        <Icon name="chevron-down" size="sm" />
                                    </button>
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>

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
                        onClick={() => onSubmit({ order: orderedIndices })}
                        disabled={isSubmitting}
                        className="flex-1 py-4 rounded-full bg-primary text-on-primary font-extrabold btn-3d disabled:opacity-40"
                    >
                        {isSubmitting ? "Проверяем..." : "Проверить"}
                    </button>
                )}
            </div>
        </div>
    );
}
