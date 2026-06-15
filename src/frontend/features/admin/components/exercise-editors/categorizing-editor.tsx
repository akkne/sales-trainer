"use client";

import { CategorizeContent, CategorizeItem, inputCls, labelCls } from "./types";

interface Props {
    content: CategorizeContent;
    onChange: (c: CategorizeContent) => void;
}

export function CategorizingEditor({ content, onChange }: Props) {
    // --- Categories ---
    function addCategory() {
        onChange({ ...content, categories: [...content.categories, `Category ${content.categories.length + 1}`] });
    }

    function updateCategory(index: number, value: string) {
        const oldValue = content.categories[index];
        const categories = content.categories.map((c, i) => i === index ? value : c);
        // Remap items that used the old category name
        const items = content.items.map((it) => it.category === oldValue ? { ...it, category: value } : it);
        onChange({ ...content, categories, items });
    }

    function removeCategory(index: number) {
        if (content.categories.length <= 2) return;
        const removed = content.categories[index];
        const categories = content.categories.filter((_, i) => i !== index);
        // Items assigned to removed category fall back to first remaining
        const fallback = categories[0] ?? "";
        const items = content.items.map((it) => it.category === removed ? { ...it, category: fallback } : it);
        onChange({ ...content, categories, items });
    }

    // --- Items ---
    function addItem() {
        onChange({ ...content, items: [...content.items, { text: "", category: content.categories[0] ?? "" }] });
    }

    function updateItemText(index: number, text: string) {
        const items = content.items.map((it, i) => i === index ? { ...it, text } : it);
        onChange({ ...content, items });
    }

    function updateItemCategory(index: number, category: string) {
        const items = content.items.map((it, i) => i === index ? { ...it, category } : it);
        onChange({ ...content, items });
    }

    function removeItem(index: number) {
        onChange({ ...content, items: content.items.filter((_, i) => i !== index) });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input
                    className={inputCls}
                    value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Categorise these items"
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Categories</span>
                    <button type="button" onClick={addCategory} className="text-xs text-ink-3 hover:text-ink">
                        + Add category
                    </button>
                </div>
                {content.categories.map((cat: string, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={cat}
                            onChange={(e) => updateCategory(i, e.target.value)}
                            placeholder={`Category ${i + 1}`}
                        />
                        {content.categories.length > 2 && (
                            <button type="button" onClick={() => removeCategory(i)} className="text-xs text-bad shrink-0">
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Items</span>
                    <button type="button" onClick={addItem} className="text-xs text-ink-3 hover:text-ink">
                        + Add item
                    </button>
                </div>
                {content.items.map((item: CategorizeItem, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={item.text}
                            onChange={(e) => updateItemText(i, e.target.value)}
                            placeholder={`Item ${i + 1}`}
                        />
                        <select
                            className={inputCls}
                            value={item.category}
                            onChange={(e) => updateItemCategory(i, e.target.value)}
                        >
                            {content.categories.map((cat) => (
                                <option key={cat} value={cat}>{cat || "(no category)"}</option>
                            ))}
                        </select>
                        <button type="button" onClick={() => removeItem(i)} className="text-xs text-bad shrink-0">
                            ×
                        </button>
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
