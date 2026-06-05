"use client";

import { MultipleChoiceContent, inputCls, labelCls } from "./types";

interface Props {
    content: MultipleChoiceContent;
    onChange: (c: MultipleChoiceContent) => void;
}

export function MultipleChoiceEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Situation (context)</span>
                <input className={inputCls} value={content.situation}
                    onChange={(e) => onChange({ ...content, situation: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>Question</span>
                <input className={inputCls} value={content.question}
                    onChange={(e) => onChange({ ...content, question: e.target.value })} />
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
                <span className={labelCls}>Explanation (shown after answer)</span>
                <input className={inputCls} value={content.explanation}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })} />
            </label>
        </div>
    );
}
