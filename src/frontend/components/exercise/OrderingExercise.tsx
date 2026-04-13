"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface OrderingItem {
    id: string;
    text: string;
}

interface OrderingContent {
    situation: string;
    items: OrderingItem[];
    correctOrder: string[];
    explanation?: string;
}

interface OrderingExerciseProps {
    content: OrderingContent;
    onSubmit: (answer: { order: string[] }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function OrderingExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: OrderingExerciseProps) {
    // Shuffle items deterministically on first render
    const shuffledItems = useMemo(() => {
        const items = [...content.items];
        // Simple shuffle based on item IDs
        return items.sort((a, b) => a.id.localeCompare(b.id));
    }, [content.items]);

    const [orderedIds, setOrderedIds] = useState<string[]>(shuffledItems.map(i => i.id));
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    function handleDragStart(index: number) {
        if (isAnswered) return;
        setDraggedIndex(index);
    }

    function handleDragOver(e: React.DragEvent, targetIndex: number) {
        e.preventDefault();
        if (draggedIndex === null || draggedIndex === targetIndex || isAnswered) return;

        const newOrder = [...orderedIds];
        const [dragged] = newOrder.splice(draggedIndex, 1);
        newOrder.splice(targetIndex, 0, dragged);
        setOrderedIds(newOrder);
        setDraggedIndex(targetIndex);
    }

    function handleDragEnd() {
        setDraggedIndex(null);
    }

    function moveItem(fromIndex: number, direction: "up" | "down") {
        if (isAnswered) return;
        const toIndex = direction === "up" ? fromIndex - 1 : fromIndex + 1;
        if (toIndex < 0 || toIndex >= orderedIds.length) return;

        const newOrder = [...orderedIds];
        [newOrder[fromIndex], newOrder[toIndex]] = [newOrder[toIndex], newOrder[fromIndex]];
        setOrderedIds(newOrder);
    }

    function itemStyle(itemId: string, index: number): string {
        const base = "flex items-center gap-3 px-4 py-3 rounded-xl text-left font-medium transition-colors border-b-4 cursor-grab active:cursor-grabbing";

        if (!isAnswered) {
            return `${base} border-outline-variant bg-surface-container text-on-surface hover:bg-surface-container-high`;
        }

        const correctIndex = content.correctOrder.indexOf(itemId);
        const isCorrectPosition = index === correctIndex;

        if (isCorrectPosition) {
            return `${base} border-primary bg-primary-container text-primary cursor-default`;
        }
        return `${base} border-error bg-error-container text-error cursor-default`;
    }

    const itemsMap = useMemo(() => {
        const map = new Map<string, OrderingItem>();
        content.items.forEach(item => map.set(item.id, item));
        return map;
    }, [content.items]);

    return (
        <div className="flex flex-col gap-6">
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="list-ordered" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Расставьте элементы в правильном порядке:
            </p>

            <div className="flex flex-col gap-2">
                {orderedIds.map((itemId, index) => {
                    const item = itemsMap.get(itemId);
                    if (!item) return null;

                    return (
                        <div
                            key={itemId}
                            draggable={!isAnswered}
                            onDragStart={() => handleDragStart(index)}
                            onDragOver={(e) => handleDragOver(e, index)}
                            onDragEnd={handleDragEnd}
                            className={itemStyle(itemId, index)}
                        >
                            <span className="w-7 h-7 rounded-full bg-surface-container-highest flex items-center justify-center text-sm font-bold shrink-0">
                                {index + 1}
                            </span>
                            <span className="flex-1">{item.text}</span>
                            {!isAnswered && (
                                <div className="flex flex-col gap-1">
                                    <button
                                        type="button"
                                        onClick={() => moveItem(index, "up")}
                                        disabled={index === 0}
                                        className="p-1 rounded hover:bg-surface-container-highest disabled:opacity-30"
                                    >
                                        <Icon name="chevron-up" size="sm" />
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => moveItem(index, "down")}
                                        disabled={index === orderedIds.length - 1}
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
                        onClick={() => onSubmit({ order: orderedIds })}
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
