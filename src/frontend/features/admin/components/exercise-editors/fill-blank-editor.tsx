"use client";

import { FillBlankContent, inputCls, labelCls } from "./types";

interface Props {
    content: FillBlankContent;
    onChange: (c: FillBlankContent) => void;
}

export function FillBlankEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Character Name</span>
                <input className={inputCls} value={content.characterName}
                    onChange={(e) => onChange({ ...content, characterName: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>Line with blank (use ___ for the gap)</span>
                <textarea rows={2} className={inputCls} value={content.characterLine}
                    onChange={(e) => onChange({ ...content, characterLine: e.target.value })} />
            </label>
            <div>
                <span className={labelCls}>Options</span>
                {content.options.map((opt, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            type="radio"
                            checked={content.correctOptionIndex === i}
                            onChange={() => onChange({ ...content, correctOptionIndex: i })}
                            className="shrink-0"
                        />
                        <input className={inputCls} value={opt}
                            onChange={(e) => {
                                const opts = [...content.options];
                                opts[i] = e.target.value;
                                onChange({ ...content, options: opts });
                            }}
                            placeholder={`Option ${i + 1}`}
                        />
                    </div>
                ))}
                <span className="text-[10px] text-on-surface-variant mt-1 block">
                    Radio button marks the correct answer
                </span>
            </div>
            <label className="block">
                <span className={labelCls}>Explanation</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })} />
            </label>
        </div>
    );
}
