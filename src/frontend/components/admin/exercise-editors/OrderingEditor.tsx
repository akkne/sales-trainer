"use client";

import { OrderingContent, inputCls, labelCls } from "./types";

interface Props {
    content: OrderingContent;
    onChange: (c: OrderingContent) => void;
}

export function OrderingEditor({ content, onChange }: Props) {
    function addItem() {
        onChange({
            ...content,
            items: [...content.items, ""],
            correctOrder: [...content.correctOrder, ""]
        });
    }

    function removeItem(index: number) {
        const items = content.items.filter((_, i) => i !== index);
        const correctOrder = content.correctOrder.filter((_, i) => i !== index);
        onChange({ ...content, items, correctOrder });
    }

    function updateItem(index: number, value: string) {
        const items = [...content.items];
        items[index] = value;
        // Also update correctOrder if it contained the old value
        const correctOrder = content.correctOrder.map(item =>
            item === content.items[index] ? value : item
        );
        onChange({ ...content, items, correctOrder });
    }

    function moveUp(index: number) {
        if (index === 0) return;
        const correctOrder = [...content.correctOrder];
        [correctOrder[index - 1], correctOrder[index]] = [correctOrder[index], correctOrder[index - 1]];
        onChange({ ...content, correctOrder });
    }

    function moveDown(index: number) {
        if (index >= content.correctOrder.length - 1) return;
        const correctOrder = [...content.correctOrder];
        [correctOrder[index], correctOrder[index + 1]] = [correctOrder[index + 1], correctOrder[index]];
        onChange({ ...content, correctOrder });
    }

    function syncCorrectOrder() {
        onChange({ ...content, correctOrder: [...content.items] });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input className={inputCls} value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Arrange these steps in the correct order" />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Items (shown shuffled to user)</span>
                    <button
                        type="button"
                        onClick={addItem}
                        className="text-xs text-on-surface-variant hover:text-on-surface"
                    >
                        + Add item
                    </button>
                </div>
                {content.items.map((item, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <span className="text-xs text-on-surface-variant w-4">{i + 1}.</span>
                        <input
                            className={inputCls}
                            value={item}
                            onChange={(e) => updateItem(i, e.target.value)}
                            placeholder={`Item ${i + 1}`}
                        />
                        {content.items.length > 2 && (
                            <button
                                type="button"
                                onClick={() => removeItem(i)}
                                className="text-xs text-error hover:text-error/80"
                            >
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Correct Order (use arrows to reorder)</span>
                    <button
                        type="button"
                        onClick={syncCorrectOrder}
                        className="text-xs text-on-surface-variant hover:text-on-surface"
                    >
                        Reset to items order
                    </button>
                </div>
                {content.correctOrder.map((item, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1 bg-surface-container-low p-2 rounded">
                        <span className="text-xs text-on-surface-variant w-4">{i + 1}.</span>
                        <span className="flex-1 text-sm text-on-surface">{item || "(empty)"}</span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                onClick={() => moveUp(i)}
                                disabled={i === 0}
                                className="text-xs text-on-surface-variant hover:text-on-surface disabled:opacity-30"
                            >
                                ↑
                            </button>
                            <button
                                type="button"
                                onClick={() => moveDown(i)}
                                disabled={i === content.correctOrder.length - 1}
                                className="text-xs text-on-surface-variant hover:text-on-surface disabled:opacity-30"
                            >
                                ↓
                            </button>
                        </div>
                    </div>
                ))}
            </div>

            <label className="block">
                <span className={labelCls}>Explanation</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })}
                    placeholder="The correct sequence is..." />
            </label>
        </div>
    );
}
