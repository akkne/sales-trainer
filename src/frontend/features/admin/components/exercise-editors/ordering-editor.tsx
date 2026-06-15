"use client";

import { ReorderContent, ReorderItem, inputCls, labelCls } from "./types";

interface Props {
    content: ReorderContent;
    onChange: (c: ReorderContent) => void;
}

export function OrderingEditor({ content, onChange }: Props) {
    function updateItemText(index: number, text: string) {
        const items = content.items.map((it, i) => i === index ? { ...it, text } : it);
        onChange({ ...content, items });
    }

    function moveUp(index: number) {
        if (index === 0) return;
        const items = [...content.items];
        const posA = items[index - 1].correct_position;
        const posB = items[index].correct_position;
        items[index - 1] = { ...items[index - 1], correct_position: posB };
        items[index] = { ...items[index], correct_position: posA };
        onChange({ ...content, items });
    }

    function moveDown(index: number) {
        if (index >= content.items.length - 1) return;
        const items = [...content.items];
        const posA = items[index].correct_position;
        const posB = items[index + 1].correct_position;
        items[index] = { ...items[index], correct_position: posB };
        items[index + 1] = { ...items[index + 1], correct_position: posA };
        onChange({ ...content, items });
    }

    function addItem() {
        const nextPos = content.items.length + 1;
        onChange({ ...content, items: [...content.items, { text: "", correct_position: nextPos }] });
    }

    function removeItem(index: number) {
        if (content.items.length <= 2) return;
        const items = content.items.filter((_, i) => i !== index);
        // Re-normalise positions to 1..n
        const sorted = [...items].sort((a, b) => a.correct_position - b.correct_position);
        sorted.forEach((it, i) => { it.correct_position = i + 1; });
        onChange({ ...content, items });
    }

    // Sort display by current correct_position for intuitive editing
    const sorted = [...content.items]
        .map((it, originalIndex) => ({ ...it, originalIndex }))
        .sort((a, b) => a.correct_position - b.correct_position);

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input
                    className={inputCls}
                    value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Arrange these steps in the correct order"
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Items (ordered by correct_position — use ↑↓ to reorder)</span>
                    <button type="button" onClick={addItem} className="text-xs text-ink-3 hover:text-ink">
                        + Add item
                    </button>
                </div>
                <p className="text-[10px] text-ink-3 mb-2">
                    The order shown here IS the correct order. Arrows swap positions between adjacent rows.
                </p>
                {sorted.map((item: ReorderItem & { originalIndex: number }, sortedIdx: number) => (
                    <div key={item.originalIndex} className="flex items-center gap-2 mt-1">
                        <span className="text-xs text-ink-3 w-5 shrink-0">{item.correct_position}.</span>
                        <input
                            className={inputCls}
                            value={item.text}
                            onChange={(e) => updateItemText(item.originalIndex, e.target.value)}
                            placeholder={`Step ${item.correct_position}`}
                        />
                        <div className="flex flex-col gap-0.5 shrink-0">
                            <button
                                type="button"
                                onClick={() => moveUp(sortedIdx)}
                                disabled={sortedIdx === 0}
                                className="text-[10px] text-ink-3 hover:text-ink disabled:opacity-30 leading-none"
                            >↑</button>
                            <button
                                type="button"
                                onClick={() => moveDown(sortedIdx)}
                                disabled={sortedIdx === sorted.length - 1}
                                className="text-[10px] text-ink-3 hover:text-ink disabled:opacity-30 leading-none"
                            >↓</button>
                        </div>
                        {content.items.length > 2 && (
                            <button type="button" onClick={() => removeItem(item.originalIndex)} className="text-xs text-bad shrink-0">
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
