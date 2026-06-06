"use client";

import { useState, useMemo } from "react";
import type { ExerciseSubmissionResult } from "@/features/exercise/hooks/use-lesson";
import { Icon } from "@/shared/components/icon";
import { Button } from "@/shared/components/button";
import { ExerciseResultBanner } from "./exercise-result-banner";

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

const PAIR_COLORS = ["var(--olive)", "var(--indigo)", "var(--rust)", "var(--clay)"];
const PAIR_BG_COLORS = ["var(--olive-soft)", "var(--indigo-soft)", "var(--rust-soft)", "var(--bg-2)"];

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
            <h2 style={{ fontSize: 22, fontWeight: 500, letterSpacing: -0.3, margin: 0, lineHeight: 1.3 }}>
                {content.instruction || "Сопоставьте пары:"}
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
                            let textColor = isSelected ? "var(--bg)" : "var(--ink)";
                            let borderColor = isSelected ? "var(--ink)" : isConnected ? PAIR_COLORS[pairIdx! % PAIR_COLORS.length] : "var(--line)";

                            if (isAnswered) {
                                const pair = userPairs.find(p => p.left === item);
                                const isCorrect = pair && correctPairsSet.has(`${pair.left}:${pair.right}`);
                                bgColor = isCorrect ? "var(--good-soft)" : "var(--bad-soft)";
                                borderColor = isCorrect ? "var(--good)" : "var(--bad)";
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
                                        fontFamily: "var(--f-sans)",
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
                                bgColor = isCorrect ? "var(--good-soft)" : pair ? "var(--bad-soft)" : "var(--bg-2)";
                                borderColor = isCorrect ? "var(--good)" : pair ? "var(--bad)" : "var(--line)";
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
                                        fontFamily: "var(--f-sans)",
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
                            fontFamily: "var(--f-mono)",
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
                <div
                    style={{
                        position: "fixed",
                        bottom: 0,
                        left: 0,
                        right: 0,
                        background: "var(--surface)",
                        borderTop: "1px solid var(--line)",
                        padding: "20px 32px",
                        paddingBottom: "max(20px, env(safe-area-inset-bottom))",
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            maxWidth: 820,
                            margin: "0 auto",
                        }}
                    >
                        {onSkip && (
                            <Button variant="ghost" onClick={onSkip} disabled={isSubmitting}>
                                ПРОПУСТИТЬ
                            </Button>
                        )}
                        <div style={{ display: "flex", alignItems: "center", gap: 16, marginLeft: "auto" }}>
                            <div
                                className="mono"
                                style={{ fontSize: 11, color: "var(--ink-4)", display: "none" }}
                                data-keyboard-hint
                            >
                                Enter — проверить
                            </div>
                            <Button
                                variant="accent"
                                size="lg"
                                onClick={() => onSubmit({ pairs: userPairs })}
                                disabled={!canSubmit || isSubmitting}
                                loading={isSubmitting}
                                iconRightName="arrow-right"
                            >
                                ПРОВЕРИТЬ
                            </Button>
                        </div>
                    </div>
                    <style jsx global>{`
                        @media (pointer: fine) {
                            [data-keyboard-hint] {
                                display: block !important;
                            }
                        }
                    `}</style>
                </div>
            )}
        </div>
    );
}
