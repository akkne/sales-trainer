"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

// The learner API strips `correct_position` from every item (docs/AUDIT_PROD.md X-3), so this
// type must not promise a field the runtime value never has (docs/AUDIT_NIGHT_REVIEW.md R2-4).
interface ReorderItem {
    text: string;
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
    submitError?: Error | null;
}

export function ReorderExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
    submitError,
}: ReorderExerciseProps) {
    // The array order the exercise content arrives in — the only order the client actually has
    // pre-submit. `correct_position` never reaches this component (see the `ReorderItem` comment
    // above), so the client fundamentally cannot know the real solved order before submitting
    // (docs/AUDIT_NIGHT_REVIEW.md R2-4): it has nothing trustworthy to check its shuffle against.
    const identityIndices = useMemo(() => content.items.map((_, i) => i), [content.items]);

    // X-2: a plain Fisher–Yates shuffle can deal the items back out in the same order they arrived
    // in, so a learner who never touches anything still sees an arrangement that happens to match
    // the input array. Since the real correct order is unknowable here (R2-4), the only thing this
    // guard can honestly guarantee is that the starting arrangement differs from the arrival order —
    // falling back to a rotation, which always differs from the original for 2+ distinct indices,
    // if the shuffle happened to land on it anyway.
    const shuffledIndices = useMemo(() => {
        const indices = [...identityIndices];
        if (indices.length <= 1) return indices; // degenerate: nothing to reorder

        for (let i = indices.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [indices[i], indices[j]] = [indices[j], indices[i]];
        }

        const isUnshuffled = indices.every(
            (value, position) => value === identityIndices[position]
        );
        if (isUnshuffled) {
            return [...indices.slice(1), indices[0]];
        }

        return indices;
    }, [identityIndices]);

    const [orderedIndices, setOrderedIndices] = useState<number[]>(shuffledIndices);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    // X-3: the learner content strips `correct_position`, so row marking after answering must use
    // the correct order the server hands back in the submission result. Pre-submit there is no
    // correct order to mark rows against at all (R2-4) — the fallback below is only ever read
    // while `isAnswered` is false, where `showCorrect`/`showWrong` are already gated off.
    const correctOrder = submittedResult?.correctAnswer?.order ?? identityIndices;

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
                            tabIndex={isAnswered ? undefined : 0}
                            aria-label={isAnswered ? undefined : `${item.text}, позиция ${position + 1} из ${orderedIndices.length}. Стрелки вверх/вниз — переместить.`}
                            onDragStart={() => handleDragStart(position)}
                            onDragOver={(e) => handleDragOver(e, position)}
                            onDragEnd={handleDragEnd}
                            onKeyDown={(e) => {
                                if (isAnswered) return;
                                if (e.key === "ArrowUp") {
                                    e.preventDefault();
                                    moveItem(position, "up");
                                } else if (e.key === "ArrowDown") {
                                    e.preventDefault();
                                    moveItem(position, "down");
                                }
                            }}
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 12,
                                padding: "12px 14px",
                                background: bgColor,
                                border: `1px solid ${borderColor}`,
                                borderRadius: 12,
                                outlineOffset: 2,
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
                                <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                                    <button
                                        onClick={() => moveItem(position, "up")}
                                        disabled={position === 0}
                                        aria-label="Выше"
                                        style={{
                                            background: "var(--bg-2)",
                                            border: "none",
                                            borderRadius: 6,
                                            width: 36,
                                            height: 28,
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
                                        aria-label="Ниже"
                                        style={{
                                            background: "var(--bg-2)",
                                            border: "none",
                                            borderRadius: 6,
                                            width: 36,
                                            height: 28,
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
                    onContinue={onContinue ?? (() => {})}
                />
            ) : (
                <ExerciseActionFooter
                    onSkip={onSkip}
                    onSubmit={() => onSubmit({ order: orderedIndices })}
                    canSubmit={true}
                    isSubmitting={isSubmitting}
                    submitError={submitError}
                    keyboardHint="↑↓ порядок · Enter — проверить"
                />
            )}
        </div>
    );
}
