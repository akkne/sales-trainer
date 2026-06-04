"use client";

import { FindErrorContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: FindErrorContent;
    onChange: (c: FindErrorContent) => void;
}

let nextLineId = 100;
let nextFixId = 100;

export function FindErrorEditor({ content, onChange }: Props) {
    function addLine() {
        const id = String(nextLineId++);
        onChange({
            ...content,
            dialogLines: [...content.dialogLines, { id, speaker: "", text: "" }]
        });
    }

    function removeLine(index: number) {
        const line = content.dialogLines[index];
        const dialogLines = content.dialogLines.filter((_, i) => i !== index);
        const errorLineId = content.errorLineId === line.id ? "" : content.errorLineId;
        onChange({ ...content, dialogLines, errorLineId });
    }

    function updateLine(index: number, field: "speaker" | "text", value: string) {
        const dialogLines = [...content.dialogLines];
        dialogLines[index] = { ...dialogLines[index], [field]: value };
        onChange({ ...content, dialogLines });
    }

    function setErrorLine(id: string) {
        onChange({
            ...content,
            errorLineId: content.errorLineId === id ? "" : id
        });
    }

    function addFix() {
        const id = String(nextFixId++);
        onChange({
            ...content,
            suggestedFixes: [...(content.suggestedFixes || []), { id, text: "" }]
        });
    }

    function removeFix(index: number) {
        const fix = content.suggestedFixes?.[index];
        const suggestedFixes = (content.suggestedFixes || []).filter((_, i) => i !== index);
        const correctFixIds = (content.correctFixIds || []).filter(id => id !== fix?.id);
        onChange({ ...content, suggestedFixes, correctFixIds });
    }

    function updateFix(index: number, text: string) {
        const suggestedFixes = [...(content.suggestedFixes || [])];
        suggestedFixes[index] = { ...suggestedFixes[index], text };
        onChange({ ...content, suggestedFixes });
    }

    function toggleCorrectFix(id: string) {
        const correctFixIds = content.correctFixIds || [];
        if (correctFixIds.includes(id)) {
            onChange({ ...content, correctFixIds: correctFixIds.filter(f => f !== id) });
        } else {
            onChange({ ...content, correctFixIds: [...correctFixIds, id] });
        }
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input className={inputCls} value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Find the mistake in this conversation" />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Dialog Lines (click to mark as error)</span>
                    <button
                        type="button"
                        onClick={addLine}
                        className="text-xs text-ink-3 hover:text-ink"
                    >
                        + Add line
                    </button>
                </div>
                {content.dialogLines.map((line, i) => (
                    <div
                        key={line.id}
                        className={`mt-2 p-2 rounded border cursor-pointer transition-colors ${
                            content.errorLineId === line.id
                                ? "border-bad bg-bad-soft"
                                : "border-line bg-surface hover:border-line-2"
                        }`}
                        onClick={() => setErrorLine(line.id)}
                    >
                        <div className="flex items-center gap-2">
                            <input
                                className="w-24 border border-line rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                value={line.speaker}
                                onChange={(e) => { e.stopPropagation(); updateLine(i, "speaker", e.target.value); }}
                                onClick={(e) => e.stopPropagation()}
                                placeholder="Speaker"
                            />
                            <input
                                className="flex-1 border border-line rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-indigo/30 bg-surface"
                                value={line.text}
                                onChange={(e) => { e.stopPropagation(); updateLine(i, "text", e.target.value); }}
                                onClick={(e) => e.stopPropagation()}
                                placeholder="Dialog text"
                            />
                            {content.dialogLines.length > 1 && (
                                <button
                                    type="button"
                                    onClick={(e) => { e.stopPropagation(); removeLine(i); }}
                                    className="text-xs text-bad hover:text-bad/80"
                                >
                                    ×
                                </button>
                            )}
                        </div>
                        {content.errorLineId === line.id && (
                            <span className="text-xs text-bad mt-1 block">← This is the error</span>
                        )}
                    </div>
                ))}
            </div>

            <label className="flex items-center gap-2">
                <input
                    type="checkbox"
                    checked={content.requireExplanation}
                    onChange={(e) => onChange({ ...content, requireExplanation: e.target.checked })}
                />
                <span className={labelCls}>Require user explanation</span>
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Suggested Fixes (optional)</span>
                    <button
                        type="button"
                        onClick={addFix}
                        className="text-xs text-ink-3 hover:text-ink"
                    >
                        + Add fix
                    </button>
                </div>
                {(content.suggestedFixes || []).map((fix, i) => (
                    <div key={fix.id} className="flex items-center gap-2 mt-1">
                        <input
                            type="checkbox"
                            checked={(content.correctFixIds || []).includes(fix.id)}
                            onChange={() => toggleCorrectFix(fix.id)}
                            title="Mark as correct fix"
                        />
                        <input
                            className={inputCls}
                            value={fix.text}
                            onChange={(e) => updateFix(i, e.target.value)}
                            placeholder="Suggested fix text"
                        />
                        <button
                            type="button"
                            onClick={() => removeFix(i)}
                            className="text-xs text-bad hover:text-bad/80"
                        >
                            ×
                        </button>
                    </div>
                ))}
                <span className="text-[10px] text-ink-3 mt-1 block">
                    Checkbox marks correct fixes
                </span>
            </div>

            <label className="block">
                <span className={labelCls}>AI Evaluation Prompt</span>
                <textarea rows={4} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Evaluate if user correctly identified the error and provided appropriate fix..." />
            </label>
        </div>
    );
}
