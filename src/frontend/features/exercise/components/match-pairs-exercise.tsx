"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { ExerciseResultBanner } from "./exercise-result-banner";
import { ExerciseActionFooter } from "./exercise-action-footer";

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

const PAIR_COLORS = ["var(--success)", "var(--primary)", "var(--flame)", "var(--violet)"];
const PAIR_BG_COLORS = ["var(--success-soft)", "var(--primary-soft)", "var(--flame-soft)", "var(--violet-soft)"];

export function MatchPairsExercise({
    content,
    onSubmit,
    onSkip,
    onContinue,
    isSubmitting,
    submittedResult,
}: MatchPairsExerciseProps) {
    const leftItems = useMemo(() => content.pairs.map(p => p.left), [content.pairs]);
    const rightItems = useMemo(() => {
        const items = content.pairs.map(p => p.right);
        return items.sort(() => Math.random() - 0.5);
    }, [content.pairs]);

    const [selectedLeft, setSelectedLeft] = useState<string | null>(null);
    const [userPairs, setUserPairs] = useState<MatchPair[]>([]);

    const isAnswered = submittedResult !== null && submittedResult !== undefined;

    const connectedLefts = useMemo(() => new Set(userPairs.map(p => p.left)), [userPairs]);
    const connectedRights = useMemo(() => new Set(userPairs.map(p => p.right)), [userPairs]);

    const pairIndexByLeft = useMemo(() => {
        const map = new Map<string, number>();
        userPairs.forEach((p, idx) => map.set(p.left, idx));
        return map;
    }, [userPairs]);

    const pairIndexByRight = useMemo(() => {
        const map = new Map<string, number>();
        userPairs.forEach((p, idx) => map.set(p.right, idx));
        return map;
    }, [userPairs]);

    function handleLeftClick(item: string) {
        if (isAnswered) return;
        if (connectedLefts.has(item)) return;
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

    const canSubmit = userPairs.length === leftItems.length;

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
            <div><span className="ex-chip ex-chip--match">Соотнеси пары</span></div>
            <h2 className="h3" style={{ margin: 0, lineHeight: 1.3 }}>
                {content.instruction || "Соотнеси пары:"}
            </h2>

            <div
                style={{
                    background: "var(--surface)",
                    border: "1px solid var(--line)",
                    borderRadius: 14,
                    padding: 20,
                }}
            >
                <div
                    style={{
                        display: "grid",
                        gridTemplateColumns: "1fr 80px 1fr",
                        gap: 16,
                        alignItems: "center",
                    }}
                >
                    {/* Left column */}
                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        <div
                            style={{
                                fontSize: 10,
                                color: "var(--ink-3)",
                                letterSpacing: 1,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 4,
                            }}
                        >
                            Возражение
                        </div>
                        {leftItems.map((item) => {
                            const isConnected = connectedLefts.has(item);
                            const pairIdx = pairIndexByLeft.get(item);
                            const isSelected = selectedLeft === item;

                            let bgColor = isSelected ? "var(--ink)" : isConnected ? PAIR_BG_COLORS[pairIdx! % PAIR_BG_COLORS.length] : "var(--bg-2)";
                            const textColor = isSelected ? "var(--bg)" : "var(--ink)";
                            let borderColor = isSelected ? "var(--ink)" : isConnected ? PAIR_COLORS[pairIdx! % PAIR_COLORS.length] : "var(--line)";

                            if (isAnswered) {
                                const pair = userPairs.find(p => p.left === item);
                                const isCorrect = pair && correctPairsSet.has(`${pair.left}:${pair.right}`);
                                bgColor = isCorrect ? "var(--success-soft)" : "var(--heart-soft)";
                                borderColor = isCorrect ? "var(--success)" : "var(--heart)";
                            }

                            return (
                                <button
                                    key={item}
                                    onClick={() => handleLeftClick(item)}
                                    disabled={isAnswered || isConnected}
                                    style={{
                                        padding: "12px 14px",
                                        background: bgColor,
                                        color: textColor,
                                        border: `1px solid ${borderColor}`,
                                        borderRadius: 10,
                                        cursor: isAnswered || isConnected ? "default" : "pointer",
                                        textAlign: "left",
                                        fontSize: 14,
                                        fontFamily: "var(--font-ui)",
                                    }}
                                >
                                    {item}
                                </button>
                            );
                        })}
                    </div>

                    <div />

                    {/* Right column */}
                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        <div
                            style={{
                                fontSize: 10,
                                color: "var(--ink-3)",
                                letterSpacing: 1,
                                textTransform: "uppercase",
                                fontWeight: 500,
                                marginBottom: 4,
                            }}
                        >
                            Техника
                        </div>
                        {rightItems.map((item) => {
                            const isConnected = connectedRights.has(item);
                            const pairIdx = pairIndexByRight.get(item);
                            const isAvailable = selectedLeft && !isConnected;

                            let bgColor = isConnected ? PAIR_BG_COLORS[pairIdx! % PAIR_BG_COLORS.length] : "var(--bg-2)";
                            let textColor = isConnected ? PAIR_COLORS[pairIdx! % PAIR_COLORS.length] : "var(--ink)";
                            let borderColor = isConnected ? PAIR_COLORS[pairIdx! % PAIR_COLORS.length] : "var(--line)";

                            if (isAnswered) {
                                const pair = userPairs.find(p => p.right === item);
                                const isCorrect = pair && correctPairsSet.has(`${pair.left}:${pair.right}`);
                                bgColor = isCorrect ? "var(--success-soft)" : pair ? "var(--heart-soft)" : "var(--bg-2)";
                                borderColor = isCorrect ? "var(--success)" : pair ? "var(--heart)" : "var(--line)";
                                textColor = "var(--ink)";
                            }

                            return (
                                <button
                                    key={item}
                                    onClick={() => handleRightClick(item)}
                                    disabled={isAnswered || isConnected || !selectedLeft}
                                    style={{
                                        padding: "12px 14px",
                                        background: bgColor,
                                        color: textColor,
                                        border: `1px solid ${borderColor}`,
                                        borderRadius: 10,
                                        cursor: isAvailable ? "pointer" : "not-allowed",
                                        opacity: selectedLeft && isConnected ? 0.6 : 1,
                                        textAlign: "left",
                                        fontSize: 14,
                                        fontFamily: "var(--font-ui)",
                                    }}
                                >
                                    {item}
                                </button>
                            );
                        })}
                    </div>
                </div>

                {!isAnswered && userPairs.length > 0 && (
                    <button
                        onClick={resetAll}
                        style={{
                            marginTop: 16,
                            background: "transparent",
                            border: "none",
                            color: "var(--ink-3)",
                            fontSize: 12,
                            cursor: "pointer",
                            fontFamily: "var(--font-mono)",
                        }}
                    >
                        ↺ Сбросить
                    </button>
                )}
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
                    onSubmit={() => onSubmit({ pairs: userPairs })}
                    canSubmit={canSubmit}
                    isSubmitting={isSubmitting}
                    keyboardHint="Enter — проверить"
                />
            )}
        </div>
    );
}
