"use client";

import { MatchPairsContent, MatchPair, inputCls, labelCls } from "./types";

interface Props {
    content: MatchPairsContent;
    onChange: (c: MatchPairsContent) => void;
}

export function MatchingEditor({ content, onChange }: Props) {
    function updateLeft(index: number, left: string) {
        const pairs = content.pairs.map((p, i) => i === index ? { ...p, left } : p);
        onChange({ ...content, pairs });
    }

    function updateRight(index: number, right: string) {
        const pairs = content.pairs.map((p, i) => i === index ? { ...p, right } : p);
        onChange({ ...content, pairs });
    }

    function addPair() {
        onChange({ ...content, pairs: [...content.pairs, { left: "", right: "" }] });
    }

    function removePair(index: number) {
        if (content.pairs.length <= 2) return;
        onChange({ ...content, pairs: content.pairs.filter((_, i) => i !== index) });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input
                    className={inputCls}
                    value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Match each item on the left with its pair on the right"
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Pairs (left → right)</span>
                    <button type="button" onClick={addPair} className="text-xs text-ink-3 hover:text-ink">
                        + Add pair
                    </button>
                </div>
                {content.pairs.map((pair: MatchPair, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={pair.left}
                            onChange={(e) => updateLeft(i, e.target.value)}
                            placeholder={`Left ${i + 1}`}
                        />
                        <span className="text-ink-3 shrink-0">→</span>
                        <input
                            className={inputCls}
                            value={pair.right}
                            onChange={(e) => updateRight(i, e.target.value)}
                            placeholder={`Right ${i + 1}`}
                        />
                        {content.pairs.length > 2 && (
                            <button type="button" onClick={() => removePair(i)} className="text-xs text-bad shrink-0">
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <label className="block">
                <span className={labelCls}>Explanation (shown after answer)</span>
                <input
                    className={inputCls}
                    value={content.explanation ?? ""}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })}
                />
            </label>
        </div>
    );
}
