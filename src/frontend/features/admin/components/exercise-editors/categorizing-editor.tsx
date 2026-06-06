"use client";

import { CategorizingContent, inputCls, labelCls } from "./types";

interface Props {
    content: CategorizingContent;
    onChange: (c: CategorizingContent) => void;
}

let nextId = 100;

export function CategorizingEditor({ content, onChange }: Props) {
    function addCategory() {
        onChange({
            ...content,
            categories: [...content.categories, `Category ${content.categories.length + 1}`]
        });
    }

    function removeCategory(index: number) {
        const removed = content.categories[index];
        const categories = content.categories.filter((_, i) => i !== index);
        const correctMapping = { ...content.correctMapping };
        for (const [key, val] of Object.entries(correctMapping)) {
            if (val === removed) delete correctMapping[key];
        }
        onChange({ ...content, categories, correctMapping });
    }

    function updateCategory(index: number, value: string) {
        const oldValue = content.categories[index];
        const categories = [...content.categories];
        categories[index] = value;
        const correctMapping = { ...content.correctMapping };
        for (const [key, val] of Object.entries(correctMapping)) {
            if (val === oldValue) correctMapping[key] = value;
        }
        onChange({ ...content, categories, correctMapping });
    }

    function addItem() {
        const id = String(nextId++);
        onChange({
            ...content,
            items: [...content.items, { id, text: "" }]
        });
    }

    function removeItem(index: number) {
        const item = content.items[index];
        const items = content.items.filter((_, i) => i !== index);
        const correctMapping = { ...content.correctMapping };
        delete correctMapping[item.id];
        onChange({ ...content, items, correctMapping });
    }

    function updateItem(index: number, text: string) {
        const items = [...content.items];
        items[index] = { ...items[index], text };
        onChange({ ...content, items });
    }

    function assignCategory(itemId: string, category: string) {
        const correctMapping = { ...content.correctMapping };
        if (correctMapping[itemId] === category) {
            delete correctMapping[itemId];
        } else {
            correctMapping[itemId] = category;
        }
        onChange({ ...content, correctMapping });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input className={inputCls} value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Sort items into the correct categories" />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Categories</span>
                    <button
                        type="button"
                        onClick={addCategory}
                        className="text-xs text-ink-3 hover:text-ink"
                    >
                        + Add category
                    </button>
                </div>
                {content.categories.map((cat, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={cat}
                            onChange={(e) => updateCategory(i, e.target.value)}
                            placeholder={`Category ${i + 1}`}
                        />
                        {content.categories.length > 2 && (
                            <button
                                type="button"
                                onClick={() => removeCategory(i)}
                                className="text-xs text-bad hover:text-bad/80"
                            >
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Items to categorize</span>
                    <button
                        type="button"
                        onClick={addItem}
                        className="text-xs text-ink-3 hover:text-ink"
                    >
                        + Add item
                    </button>
                </div>
                {content.items.map((item, i) => (
                    <div key={item.id} className="mt-2 p-2 bg-surface rounded">
                        <div className="flex items-center gap-2">
                            <input
                                className={inputCls}
                                value={item.text}
                                onChange={(e) => updateItem(i, e.target.value)}
                                placeholder={`Item text`}
                            />
                            {content.items.length > 1 && (
                                <button
                                    type="button"
                                    onClick={() => removeItem(i)}
                                    className="text-xs text-bad hover:text-bad/80"
                                >
                                    ×
                                </button>
                            )}
                        </div>
                        <div className="flex gap-1 mt-2">
                            {content.categories.filter(c => c).map(cat => (
                                <button
                                    key={cat}
                                    type="button"
                                    onClick={() => assignCategory(item.id, cat)}
                                    className={`px-2 py-1 text-xs rounded border transition-colors ${
                                        content.correctMapping[item.id] === cat
                                            ? "bg-ink text-bg border-indigo"
                                            : "border-line text-ink-3 hover:bg-bg-2"
                                    }`}
                                >
                                    {cat}
                                </button>
                            ))}
                        </div>
                    </div>
                ))}
            </div>

            <label className="block">
                <span className={labelCls}>Explanation</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })}
                    placeholder="Items belong to these categories because..." />
            </label>
        </div>
    );
}
