"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";

interface CategorizeItem {
    text: string;
    category: string;
}

interface CategorizeContent {
    instruction: string;
    categories: string[];
    items: CategorizeItem[];
    explanation?: string;
}

interface CategorizeExerciseProps {
    content: CategorizeContent;
    onSubmit: (answer: { mapping: Record<number, string> }) => void;
    onSkip?: () => void;
    onContinue?: () => void;
    isSubmitting: boolean;
    submittedResult?: ExerciseSubmissionResult | null;
}

// Colors for categories
const CATEGORY_COLORS = ["#58CC02", "#FF4B4B", "#1CB0F6", "#FF9600"];

export function CategorizeExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: CategorizeExerciseProps) {
    const [mapping, setMapping] = useState<Record<number, string>>({});
    const [draggedItemIndex, setDraggedItemIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    const unplacedItems = useMemo(() => {
        return content.items.map((item, idx) => ({ ...item, idx })).filter(item => mapping[item.idx] === undefined);
    }, [content.items, mapping]);

    const itemsByCategory = useMemo(() => {
        const result: Record<string, Array<{ text: string; idx: number }>> = {};
        content.categories.forEach(cat => result[cat] = []);

        content.items.forEach((item, idx) => {
            const category = mapping[idx];
            if (category && result[category]) {
                result[category].push({ text: item.text, idx });
            }
        });

        return result;
    }, [content.items, content.categories, mapping]);

    function handleDragStart(itemIdx: number) {
        if (isAnswered) return;
        setDraggedItemIndex(itemIdx);
    }

    function handleDrop(categoryName: string) {
        if (isAnswered || draggedItemIndex === null) return;
        setMapping({ ...mapping, [draggedItemIndex]: categoryName });
        setDraggedItemIndex(null);
    }

    function handleDragOver(e: React.DragEvent) {
        e.preventDefault();
    }

    function removeFromCategory(itemIdx: number) {
        if (isAnswered) return;
        const newMapping = { ...mapping };
        delete newMapping[itemIdx];
        setMapping(newMapping);
    }

    function handleItemClick(itemIdx: number, currentCategory?: string) {
        if (isAnswered) return;

        if (currentCategory) {
            const categoryIndex = content.categories.indexOf(currentCategory);
            if (categoryIndex < content.categories.length - 1) {
                setMapping({ ...mapping, [itemIdx]: content.categories[categoryIndex + 1] });
            } else {
                removeFromCategory(itemIdx);
            }
        } else {
            if (content.categories.length > 0) {
                setMapping({ ...mapping, [itemIdx]: content.categories[0] });
            }
        }
    }

    function itemStyle(itemIdx: number): string {
        const base = "px-3 py-2 rounded-lg text-sm font-medium cursor-pointer transition-all";

        if (!isAnswered) {
            return `${base} bg-surface-container-high text-on-surface hover:bg-surface-container-highest active:scale-95`;
        }

        const userCategory = mapping[itemIdx];
        const correctCategory = content.items[itemIdx].category;

        if (userCategory === correctCategory) {
            return `${base} bg-primary-container text-primary`;
        }
        return `${base} bg-error-container text-error`;
    }

    const canSubmit = Object.keys(mapping).length === content.items.length;

    return (
        <div className="flex flex-col gap-6">
            {content.instruction && (
                <div className="flex items-start gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary-container flex items-center justify-center shrink-0 mt-1">
                        <Icon name="folders" size="sm" className="text-primary" />
                    </div>
                    <div className="relative bg-surface-container rounded-2xl rounded-tl-sm px-4 py-3 flex-1">
                        <p className="text-sm text-on-surface-variant">{content.instruction}</p>
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
                            key={item.idx}
                            draggable={!isAnswered}
                            onDragStart={() => handleDragStart(item.idx)}
                            onClick={() => handleItemClick(item.idx)}
                            className={itemStyle(item.idx)}
                        >
                            {item.text}
                        </div>
                    ))}
                </div>
            )}

            {/* Categories */}
            <div className={`grid gap-4 ${content.categories.length === 2 ? "grid-cols-2" : "grid-cols-3"}`}>
                {content.categories.map((category, catIdx) => {
                    const color = CATEGORY_COLORS[catIdx % CATEGORY_COLORS.length];
                    return (
                        <div
                            key={category}
                            onDragOver={handleDragOver}
                            onDrop={() => handleDrop(category)}
                            className="flex flex-col gap-2 p-3 rounded-xl border-2 border-dashed min-h-[120px]"
                            style={{ borderColor: color + "80", backgroundColor: color + "10" }}
                        >
                            <h4
                                className="font-bold text-sm text-center"
                                style={{ color }}
                            >
                                {category}
                            </h4>
                            <div className="flex flex-wrap gap-2">
                                {itemsByCategory[category]?.map((item) => (
                                    <div
                                        key={item.idx}
                                        draggable={!isAnswered}
                                        onDragStart={() => handleDragStart(item.idx)}
                                        onClick={() => handleItemClick(item.idx, category)}
                                        className={itemStyle(item.idx)}
                                    >
                                        {item.text}
                                        {!isAnswered && (
                                            <button
                                                type="button"
                                                onClick={(e) => { e.stopPropagation(); removeFromCategory(item.idx); }}
                                                className="ml-1 text-xs opacity-50 hover:opacity-100"
                                            >
                                                ✕
                                            </button>
                                        )}
                                    </div>
                                ))}
                            </div>
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
