"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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

const CATEGORY_COLORS = [
    { color: "oklch(0.58 0.10 120)", bg: "oklch(0.92 0.04 120)" },
    { color: "oklch(0.62 0.14 42)", bg: "oklch(0.92 0.04 42)" },
    { color: "oklch(0.50 0.18 270)", bg: "oklch(0.92 0.06 270)" },
];

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
    // tap-to-place fallback: HTML5 drag-and-drop does not fire on touch screens
    const [selectedItemIndex, setSelectedItemIndex] = useState<number | null>(null);

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

    function assignToCategory(itemIdx: number, categoryName: string) {
        if (isAnswered) return;
        setMapping({ ...mapping, [itemIdx]: categoryName });
        setSelectedItemIndex(null);
    }

    function toggleItemSelection(itemIdx: number) {
        if (isAnswered) return;
        setSelectedItemIndex(itemIdx === selectedItemIndex ? null : itemIdx);
    }

    function handleCategoryClick(categoryName: string) {
        if (isAnswered || selectedItemIndex === null) return;
        assignToCategory(selectedItemIndex, categoryName);
    }

    const canSubmit = Object.keys(mapping).length === content.items.length;

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <div><span className="ex-chip ex-chip--categorize">Категоризация</span></div>
            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                {content.instruction || "Распредели реплики по типу:"}
            </h2>

            {/* Unplaced items */}
            {!isAnswered && unplacedItems.length > 0 && (
                <div
                    style={{
                        padding: 14,
                        background: "var(--surface)",
                        border: "1px dashed var(--line-2)",
                        borderRadius: 12,
                    }}
                >
                    <div
                        style={{
                            fontSize: 10,
                            color: "var(--ink-3)",
                            letterSpacing: 1,
                            textTransform: "uppercase",
                            fontWeight: 500,
                            marginBottom: 8,
                        }}
                    >
                        ЕЩЁ НЕ РАСПРЕДЕЛЕНО — НАЖМИ РЕПЛИКУ, ЗАТЕМ КАТЕГОРИЮ
                    </div>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                        {unplacedItems.map((item) => {
                            const isSelected = selectedItemIndex === item.idx;
                            return (
                                <div
                                    key={item.idx}
                                    role="button"
                                    tabIndex={0}
                                    aria-pressed={isSelected}
                                    draggable
                                    onDragStart={() => handleDragStart(item.idx)}
                                    onClick={() => toggleItemSelection(item.idx)}
                                    onKeyDown={(e) => {
                                        if (e.key === "Enter" || e.key === " ") {
                                            e.preventDefault();
                                            toggleItemSelection(item.idx);
                                        }
                                    }}
                                    style={{
                                        padding: "10px 14px",
                                        background: isSelected ? "var(--primary-softer)" : "var(--bg-2)",
                                        border: isSelected ? "1.5px solid var(--primary)" : "1.5px solid transparent",
                                        borderRadius: 20,
                                        fontSize: 13,
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 8,
                                        cursor: "pointer",
                                    }}
                                >
                                    {item.text}
                                    <span className="cat-assign-keys">
                                        {content.categories.map((cat, catIdx) => {
                                            const catColor = CATEGORY_COLORS[catIdx % CATEGORY_COLORS.length];
                                            return (
                                                <button
                                                    key={cat}
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        assignToCategory(item.idx, cat);
                                                    }}
                                                    title={cat}
                                                    style={{
                                                        width: 20,
                                                        height: 20,
                                                        borderRadius: 6,
                                                        border: "none",
                                                        background: catColor.color,
                                                        cursor: "pointer",
                                                        fontSize: 10,
                                                        color: "white",
                                                        fontWeight: 600,
                                                    }}
                                                >
                                                    {cat[0]}
                                                </button>
                                            );
                                        })}
                                    </span>
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* Category columns */}
            <div className="cat-grid">
                {content.categories.map((category, catIdx) => {
                    const catColor = CATEGORY_COLORS[catIdx % CATEGORY_COLORS.length];
                    const isDropTarget = selectedItemIndex !== null && !isAnswered;
                    return (
                        <div
                            key={category}
                            onDragOver={handleDragOver}
                            onDrop={() => handleDrop(category)}
                            onClick={() => handleCategoryClick(category)}
                            style={{
                                padding: 14,
                                background: "var(--surface)",
                                border: "1px solid var(--line)",
                                borderTop: `3px solid ${catColor.color}`,
                                borderRadius: 12,
                                minHeight: 140,
                                outline: isDropTarget ? "2px dashed var(--primary-tint-border)" : "none",
                                outlineOffset: 2,
                                cursor: isDropTarget ? "pointer" : "default",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 12,
                                    fontWeight: 500,
                                    color: catColor.color,
                                    marginBottom: 10,
                                }}
                            >
                                {category}
                            </div>
                            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                                {itemsByCategory[category]?.map((item) => {
                                    const isCorrect = content.items[item.idx].category === category;
                                    let itemBg = "var(--bg-2)";
                                    if (isAnswered) {
                                        itemBg = isCorrect ? "var(--success-soft)" : "var(--heart-soft)";
                                    }
                                    return (
                                        <div
                                            key={item.idx}
                                            draggable={!isAnswered}
                                            onDragStart={() => handleDragStart(item.idx)}
                                            style={{
                                                padding: "8px 10px",
                                                background: itemBg,
                                                borderRadius: 8,
                                                fontSize: 13,
                                                cursor: isAnswered ? "default" : "grab",
                                                display: "flex",
                                                justifyContent: "space-between",
                                                alignItems: "center",
                                            }}
                                        >
                                            {item.text}
                                            {!isAnswered && (
                                                <button
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        removeFromCategory(item.idx);
                                                    }}
                                                    aria-label="Убрать из категории"
                                                    style={{
                                                        background: "transparent",
                                                        border: "none",
                                                        color: "var(--ink-4)",
                                                        cursor: "pointer",
                                                        fontSize: 13,
                                                        padding: "6px 8px",
                                                        margin: "-6px -8px",
                                                        flex: "none",
                                                    }}
                                                >
                                                    ✕
                                                </button>
                                            )}
                                        </div>
                                    );
                                })}
                            </div>
                        </div>
                    );
                })}
            </div>

            {/* Footer */}
            {isAnswered ? (
                <ExerciseResultBanner
                    isCorrect={submittedResult.isCorrect}
                    score={submittedResult.score}
                    explanation={submittedResult.explanation ?? null}
                    aiFeedback={submittedResult.aiFeedback ?? null}
                    xpEarned={submittedResult.xpEarned}
                    onContinue={onContinue ?? (() => {})}
                />
            ) : (
                <ExerciseActionFooter
                    onSkip={onSkip}
                    onSubmit={() => onSubmit({ mapping })}
                    canSubmit={canSubmit}
                    isSubmitting={isSubmitting}
                    keyboardHint="Enter — проверить"
                />
            )}
        </div>
    );
}
