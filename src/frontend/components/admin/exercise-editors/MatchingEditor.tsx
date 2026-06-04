"use client";

import { MatchingContent, inputCls, labelCls } from "./types";

interface Props {
    content: MatchingContent;
    onChange: (c: MatchingContent) => void;
}

export function MatchingEditor({ content, onChange }: Props) {
    function addPair() {
        onChange({
            ...content,
            leftItems: [...content.leftItems, ""],
            rightItems: [...content.rightItems, ""]
        });
    }

    function removePair(index: number) {
        const leftItems = content.leftItems.filter((_, i) => i !== index);
        const rightItems = content.rightItems.filter((_, i) => i !== index);
        const correctPairs = content.correctPairs.filter(
            p => leftItems.includes(p.left) && rightItems.includes(p.right)
        );
        onChange({ ...content, leftItems, rightItems, correctPairs });
    }

    function updateLeft(index: number, value: string) {
        const oldValue = content.leftItems[index];
        const leftItems = [...content.leftItems];
        leftItems[index] = value;
        const correctPairs = content.correctPairs.map(p =>
            p.left === oldValue ? { ...p, left: value } : p
        );
        onChange({ ...content, leftItems, correctPairs });
    }

    function updateRight(index: number, value: string) {
        const oldValue = content.rightItems[index];
        const rightItems = [...content.rightItems];
        rightItems[index] = value;
        const correctPairs = content.correctPairs.map(p =>
            p.right === oldValue ? { ...p, right: value } : p
        );
        onChange({ ...content, rightItems, correctPairs });
    }

    function togglePair(left: string, right: string) {
        const existing = content.correctPairs.find(p => p.left === left);
        let correctPairs: MatchingContent["correctPairs"];

        if (existing?.right === right) {
            // Remove pair
            correctPairs = content.correctPairs.filter(p => p.left !== left);
        } else {
            // Replace or add pair
            correctPairs = content.correctPairs.filter(p => p.left !== left);
            correctPairs.push({ left, right });
        }
        onChange({ ...content, correctPairs });
    }

    function getPairForLeft(left: string): string | null {
        return content.correctPairs.find(p => p.left === left)?.right ?? null;
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input className={inputCls} value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Match each item on the left with its pair on the right" />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Items (Left / Right)</span>
                    <button
                        type="button"
                        onClick={addPair}
                        className="text-xs text-ink-3 hover:text-ink"
                    >
                        + Add pair
                    </button>
                </div>
                {content.leftItems.map((left, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={left}
                            onChange={(e) => updateLeft(i, e.target.value)}
                            placeholder={`Left ${i + 1}`}
                        />
                        <span className="text-ink-3">→</span>
                        <input
                            className={inputCls}
                            value={content.rightItems[i]}
                            onChange={(e) => updateRight(i, e.target.value)}
                            placeholder={`Right ${i + 1}`}
                        />
                        {content.leftItems.length > 2 && (
                            <button
                                type="button"
                                onClick={() => removePair(i)}
                                className="text-xs text-bad hover:text-bad/80"
                            >
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <div>
                <span className={labelCls}>Correct Pairs (click to toggle)</span>
                <div className="mt-2 grid gap-2">
                    {content.leftItems.filter(l => l).map(left => (
                        <div key={left} className="flex items-center gap-2">
                            <span className="text-sm text-ink w-32 truncate">{left}</span>
                            <span className="text-ink-3">→</span>
                            <div className="flex gap-1 flex-wrap">
                                {content.rightItems.filter(r => r).map(right => (
                                    <button
                                        key={right}
                                        type="button"
                                        onClick={() => togglePair(left, right)}
                                        className={`px-2 py-1 text-xs rounded border transition-colors ${
                                            getPairForLeft(left) === right
                                                ? "bg-ink text-bg border-indigo"
                                                : "border-line text-ink-3 hover:bg-bg-2"
                                        }`}
                                    >
                                        {right}
                                    </button>
                                ))}
                            </div>
                        </div>
                    ))}
                </div>
            </div>

            <label className="block">
                <span className={labelCls}>Explanation</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })}
                    placeholder="These pairs match because..." />
            </label>
        </div>
    );
}
