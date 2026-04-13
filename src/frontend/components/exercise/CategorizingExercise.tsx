"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/lib/hooks/useLesson";
import { Icon } from "@/components/ui/Icon";

interface CategorizingItem {
    id: string;
    text: string;
}

interface Category {
    id: string;
    title: string;
    color: string;
}

interface CategorizingContent {
    situation: string;
    items: CategorizingItem[];
    categories: Category[];
    correctMapping: Record<string, string>;
    explanation?: string;
}

interface CategorizingExerciseProps {
    content: CategorizingContent;
    onSubmit: (answer: { mapping: Record<string, string> }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

export function CategorizingExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: CategorizingExerciseProps) {
    const [mapping, setMapping] = useState<Record<string, string>>({});
    const [draggedItemId, setDraggedItemId] = useState<string | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    const unplacedItems = useMemo(() => {
        return content.items.filter(item => !mapping[item.id]);
    }, [content.items, mapping]);

    const itemsByCategory = useMemo(() => {
        const result: Record<string, CategorizingItem[]> = {};
        content.categories.forEach(cat => result[cat.id] = []);

        content.items.forEach(item => {
            const categoryId = mapping[item.id];
            if (categoryId && result[categoryId]) {
                result[categoryId].push(item);
            }
        });

        return result;
    }, [content.items, content.categories, mapping]);

    function handleDragStart(itemId: string) {
        if (isAnswered) return;
        setDraggedItemId(itemId);
    }

    function handleDrop(categoryId: string) {
        if (isAnswered || !draggedItemId) return;
        setMapping({ ...mapping, [draggedItemId]: categoryId });
        setDraggedItemId(null);
    }

    function handleDragOver(e: React.DragEvent) {
        e.preventDefault();
    }

    function removeFromCategory(itemId: string) {
        if (isAnswered) return;
        const newMapping = { ...mapping };
        delete newMapping[itemId];
        setMapping(newMapping);
    }

    function handleItemClick(itemId: string, currentCategoryId?: string) {
        if (isAnswered) return;

        if (currentCategoryId) {
            // Item is in a category - move to next category or back to pool
            const categoryIndex = content.categories.findIndex(c => c.id === currentCategoryId);
            if (categoryIndex < content.categories.length - 1) {
                setMapping({ ...mapping, [itemId]: content.categories[categoryIndex + 1].id });
            } else {
                removeFromCategory(itemId);
            }
        } else {
            // Item is in pool - move to first category
            if (content.categories.length > 0) {
                setMapping({ ...mapping, [itemId]: content.categories[0].id });
            }
        }
    }

    function itemStyle(itemId: string): string {
        const base = "px-3 py-2 rounded-lg text-sm font-medium cursor-pointer transition-all";

        if (!isAnswered) {
            return `${base} bg-surface-container-high text-on-surface hover:bg-surface-container-highest active:scale-95`;
        }

        const userCategory = mapping[itemId];
        const correctCategory = content.correctMapping[itemId];

        if (userCategory === correctCategory) {
            return `${base} bg-primary-container text-primary`;
        }
        return `${base} bg-error-container text-error`;
    }

    const canSubmit = Object.keys(mapping).length === content.items.length;

    return (
        <div className="flex flex-col gap-6">
            {content.situation && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="folders" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.situation}</p>
                    </div>
                </div>
            )}

            <p className="font-headline font-bold text-lg text-on-surface">
                Распределите элементы по категориям:
            </p>

            {/* Unplaced items pool */}
            {!isAnswered && unplacedItems.length > 0 && (
                <div className="flex flex-wrap gap-2 p-3 bg-surface-container rounded-xl min-h-[60px]">
                    {unplacedItems.map((item) => (
                        <div
                            key={item.id}
                            draggable={!isAnswered}
                            onDragStart={() => handleDragStart(item.id)}
                            onClick={() => handleItemClick(item.id)}
                            className={itemStyle(item.id)}
                        >
                            {item.text}
                        </div>
                    ))}
                </div>
            )}

            {/* Categories */}
            <div className={`grid gap-4 ${content.categories.length === 2 ? "grid-cols-2" : "grid-cols-3"}`}>
                {content.categories.map((category) => (
                    <div
                        key={category.id}
                        onDragOver={handleDragOver}
                        onDrop={() => handleDrop(category.id)}
                        className="flex flex-col gap-2 p-3 rounded-xl border-2 border-dashed min-h-[120px]"
                        style={{ borderColor: category.color + "80", backgroundColor: category.color + "10" }}
                    >
                        <h4
                            className="font-bold text-sm text-center"
                            style={{ color: category.color }}
                        >
                            {category.title}
                        </h4>
                        <div className="flex flex-wrap gap-2">
                            {itemsByCategory[category.id]?.map((item) => (
                                <div
                                    key={item.id}
                                    draggable={!isAnswered}
                                    onDragStart={() => handleDragStart(item.id)}
                                    onClick={() => handleItemClick(item.id, category.id)}
                                    className={itemStyle(item.id)}
                                >
                                    {item.text}
                                    {!isAnswered && (
                                        <button
                                            type="button"
                                            onClick={(e) => { e.stopPropagation(); removeFromCategory(item.id); }}
                                            className="ml-1 text-xs opacity-50 hover:opacity-100"
                                        >
                                            ✕
                                        </button>
                                    )}
                                </div>
                            ))}
                        </div>
                    </div>
                ))}
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
                        onClick={() => onSubmit({ mapping })}
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
