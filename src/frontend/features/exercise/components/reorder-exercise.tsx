"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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
    const shuffledIndices = useMemo(() => {
        const indices = content.items.map((_, i) => i);
        for (let i = indices.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [indices[i], indices[j]] = [indices[j], indices[i]];
        }
        return indices;
    }, [content.items]);

    const [orderedIndices, setOrderedIndices] = useState<number[]>(shuffledIndices);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

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

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <div><span className="ex-chip ex-chip--reorder">Расставь по порядку</span></div>
            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                {content.instruction || "Расставь элементы в правильном порядке:"}
            </h2>

            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {orderedIndices.map((itemIdx, position) => {
                    const item = content.items[itemIdx];
                    if (!item) return null;

                    const correctPosition = correctOrder.indexOf(itemIdx);
                    const isCorrectPosition = position === correctPosition;
                    const showCorrect = isAnswered && isCorrectPosition;
                    const showWrong = isAnswered && !isCorrectPosition;

                    let bgColor = "var(--surface)";
                    let borderColor = "var(--line)";
                    let badgeBg = "var(--ink)";
                    let badgeColor = "var(--bg)";

                    if (showCorrect) {
                        bgColor = "var(--success-soft)";
                        borderColor = "var(--success)";
                        badgeBg = "var(--success)";
                        badgeColor = "white";
                    } else if (showWrong) {
                        bgColor = "var(--heart-soft)";
                        borderColor = "var(--heart)";
                        badgeBg = "var(--heart)";
                        badgeColor = "white";
                    }

                    return (
                        <div
                            key={itemIdx}
                            draggable={!isAnswered}
                            onDragStart={() => handleDragStart(position)}
                            onDragOver={(e) => handleDragOver(e, position)}
                            onDragEnd={handleDragEnd}
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 12,
                                padding: "12px 14px",
                                background: bgColor,
                                border: `1px solid ${borderColor}`,
                                borderRadius: 12,
                                cursor: isAnswered ? "default" : "grab",
                            }}
                        >
                            <div
                                style={{
                                    width: 28,
                                    height: 28,
                                    borderRadius: 8,
                                    background: badgeBg,
                                    color: badgeColor,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    fontSize: 13,
                                    fontFamily: "var(--font-mono)",
                                    fontWeight: 500,
                                }}
                            >
                                {position + 1}
                            </div>
                            <div style={{ flex: 1, fontSize: 14 }}>{item.text}</div>
                            {!isAnswered && (
                                <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                                    <button
                                        onClick={() => moveItem(position, "up")}
                                        disabled={position === 0}
                                        style={{
                                            background: "var(--bg-2)",
                                            border: "none",
                                            borderRadius: 6,
                                            width: 28,
                                            height: 22,
                                            cursor: "pointer",
                                            color: position === 0 ? "var(--ink-4)" : "var(--ink-2)",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "center",
                                        }}
                                    >
                                        <Icon name="chevron-up" size="xs" />
                                    </button>
                                    <button
                                        onClick={() => moveItem(position, "down")}
                                        disabled={position === orderedIndices.length - 1}
                                        style={{
                                            background: "var(--bg-2)",
                                            border: "none",
                                            borderRadius: 6,
                                            width: 28,
                                            height: 22,
                                            cursor: "pointer",
                                            color: position === orderedIndices.length - 1 ? "var(--ink-4)" : "var(--ink-2)",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "center",
                                        }}
                                    >
                                        <Icon name="chevron-down" size="xs" />
                                    </button>
                                </div>
                            )}
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
                    onSubmit={() => onSubmit({ order: orderedIndices })}
                    canSubmit={true}
                    isSubmitting={isSubmitting}
                    keyboardHint="↑↓ порядок · Enter — проверить"
                />
            )}
        </div>
    );
}
