"use client";

import { SpotMistakeContent, DialogueLine, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: SpotMistakeContent;
    onChange: (c: SpotMistakeContent) => void;
}

export function FindErrorEditor({ content, onChange }: Props) {
    function setMistake(index: number) {
        const dialogue = content.dialogue.map((line, i) => ({ ...line, is_mistake: i === index }));
        onChange({ ...content, dialogue });
    }

    function updateSpeaker(index: number, speaker: string) {
        const dialogue = content.dialogue.map((line, i) => i === index ? { ...line, speaker } : line);
        onChange({ ...content, dialogue });
    }

    function updateText(index: number, text: string) {
        const dialogue = content.dialogue.map((line, i) => i === index ? { ...line, text } : line);
        onChange({ ...content, dialogue });
    }

    function addLine() {
        onChange({
            ...content,
            dialogue: [...content.dialogue, { speaker: "seller", text: "", is_mistake: false }],
        });
    }

    function removeLine(index: number) {
        if (content.dialogue.length <= 2) return;
        const dialogue = content.dialogue.filter((_, i) => i !== index);
        // If removed line was the mistake, clear all is_mistake flags (author must re-pick)
        const hadMistake = content.dialogue[index].is_mistake;
        const result = hadMistake ? dialogue.map((l) => ({ ...l, is_mistake: false })) : dialogue;
        onChange({ ...content, dialogue: result });
    }

    return (
        <div className="space-y-3">
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Dialogue — radio marks the single mistaken line</span>
                    <button type="button" onClick={addLine} className="text-xs text-ink-3 hover:text-ink">
                        + Add line
                    </button>
                </div>
                {content.dialogue.map((line: DialogueLine, i: number) => (
                    <div
                        key={i}
                        className={`flex items-start gap-2 mt-1 p-2 rounded-md border ${
                            line.is_mistake ? "border-bad/40 bg-bad/5" : "border-line"
                        }`}
                    >
                        <input
                            type="radio"
                            checked={line.is_mistake}
                            onChange={() => setMistake(i)}
                            className="shrink-0 mt-2"
                            title="Mark as the mistake"
                        />
                        <select
                            className="mt-1 border border-line rounded-md px-2 py-1.5 text-xs bg-surface focus:outline-none focus:ring-1 focus:ring-indigo/30 shrink-0"
                            value={line.speaker}
                            onChange={(e) => updateSpeaker(i, e.target.value)}
                        >
                            <option value="seller">seller</option>
                            <option value="client">client</option>
                        </select>
                        <input
                            className={`${inputCls} flex-1`}
                            value={line.text}
                            onChange={(e) => updateText(i, e.target.value)}
                            placeholder="Line text…"
                        />
                        {content.dialogue.length > 2 && (
                            <button type="button" onClick={() => removeLine(i)} className="text-xs text-bad mt-2 shrink-0">
                                ×
                            </button>
                        )}
                    </div>
                ))}
                <p className="text-[10px] text-ink-3 mt-1">Exactly one line must be the mistake.</p>
            </div>

            <label className="block">
                <span className={labelCls}>Explanation (shown after answer)</span>
                <textarea
                    rows={2}
                    className={textareaCls}
                    value={content.explanation ?? ""}
                    onChange={(e) => onChange({ ...content, explanation: e.target.value })}
                />
            </label>

            <label className="block">
                <span className={labelCls}>Per-exercise AI prompt (optional addendum)</span>
                <textarea
                    rows={2}
                    className={textareaCls}
                    value={content.ai_prompt ?? ""}
                    onChange={(e) => onChange({ ...content, ai_prompt: e.target.value })}
                    placeholder="Extra instructions appended to the global type prompt for AI grading…"
                />
            </label>
        </div>
    );
}
