"use client";

import { FillBlankContent, FlaggedOption, inputCls, labelCls } from "./types";

interface Props {
    content: FillBlankContent;
    onChange: (c: FillBlankContent) => void;
}

export function FillBlankEditor({ content, onChange }: Props) {
    function setCorrect(index: number) {
        const options = content.options.map((o, i) => ({ ...o, is_correct: i === index }));
        onChange({ ...content, options });
    }

    function updateText(index: number, text: string) {
        const options = content.options.map((o, i) => i === index ? { ...o, text } : o);
        onChange({ ...content, options });
    }

    function addOption() {
        onChange({ ...content, options: [...content.options, { text: "", is_correct: false }] });
    }

    function removeOption(index: number) {
        if (content.options.length <= 2) return;
        const options = content.options.filter((_, i) => i !== index);
        const hasCorrect = options.some((o) => o.is_correct);
        onChange({ ...content, options: hasCorrect ? options : options.map((o, i) => ({ ...o, is_correct: i === 0 })) });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Before (dialogue lines before the blank)</span>
                <textarea
                    rows={2}
                    className={inputCls}
                    value={content.before}
                    onChange={(e) => onChange({ ...content, before: e.target.value })}
                    placeholder="Client: We already have a supplier."
                />
            </label>

            <label className="block">
                <span className={labelCls}>After (dialogue lines after the blank)</span>
                <textarea
                    rows={2}
                    className={inputCls}
                    value={content.after}
                    onChange={(e) => onChange({ ...content, after: e.target.value })}
                    placeholder="Client: Well, I suppose we could discuss it."
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Options — radio marks the correct answer</span>
                    <button type="button" onClick={addOption} className="text-xs text-ink-3 hover:text-ink">
                        + Add option
                    </button>
                </div>
                {content.options.map((opt: FlaggedOption, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            type="radio"
                            name={`fb-correct-${i}`}
                            checked={opt.is_correct}
                            onChange={() => setCorrect(i)}
                            className="shrink-0"
                        />
                        <input
                            className={inputCls}
                            value={opt.text}
                            onChange={(e) => updateText(i, e.target.value)}
                            placeholder={`Option ${i + 1}`}
                        />
                        {content.options.length > 2 && (
                            <button type="button" onClick={() => removeOption(i)} className="text-xs text-bad">
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
