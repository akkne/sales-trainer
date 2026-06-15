"use client";

import { EvaluateCallContent, TranscriptLine, EvaluationAxis, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: EvaluateCallContent;
    onChange: (c: EvaluateCallContent) => void;
}

export function RateCallEditor({ content, onChange }: Props) {
    // --- Transcript ---
    function addTranscriptLine() {
        onChange({
            ...content,
            transcript: [...content.transcript, { speaker: "seller", text: "" }],
        });
    }

    function updateTranscriptSpeaker(index: number, speaker: string) {
        const transcript = content.transcript.map((line, i) => i === index ? { ...line, speaker } : line);
        onChange({ ...content, transcript });
    }

    function updateTranscriptText(index: number, text: string) {
        const transcript = content.transcript.map((line, i) => i === index ? { ...line, text } : line);
        onChange({ ...content, transcript });
    }

    function removeTranscriptLine(index: number) {
        if (content.transcript.length <= 1) return;
        onChange({ ...content, transcript: content.transcript.filter((_, i) => i !== index) });
    }

    // --- Evaluation axes ---
    function addAxis() {
        onChange({ ...content, evaluation_axes: [...content.evaluation_axes, { name: "", description: "" }] });
    }

    function updateAxisName(index: number, name: string) {
        const evaluation_axes = content.evaluation_axes.map((ax, i) => i === index ? { ...ax, name } : ax);
        onChange({ ...content, evaluation_axes });
    }

    function updateAxisDescription(index: number, description: string) {
        const evaluation_axes = content.evaluation_axes.map((ax, i) => i === index ? { ...ax, description } : ax);
        onChange({ ...content, evaluation_axes });
    }

    function removeAxis(index: number) {
        if (content.evaluation_axes.length <= 1) return;
        onChange({ ...content, evaluation_axes: content.evaluation_axes.filter((_, i) => i !== index) });
    }

    return (
        <div className="space-y-4">
            {/* Transcript */}
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Transcript</span>
                    <button type="button" onClick={addTranscriptLine} className="text-xs text-ink-3 hover:text-ink">
                        + Add line
                    </button>
                </div>
                {content.transcript.map((line: TranscriptLine, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <select
                            className="border border-line rounded-md px-2 py-1.5 text-xs bg-surface focus:outline-none focus:ring-1 focus:ring-indigo/30 shrink-0"
                            value={line.speaker}
                            onChange={(e) => updateTranscriptSpeaker(i, e.target.value)}
                        >
                            <option value="seller">seller</option>
                            <option value="client">client</option>
                        </select>
                        <input
                            className={`${inputCls} flex-1`}
                            value={line.text}
                            onChange={(e) => updateTranscriptText(i, e.target.value)}
                            placeholder="Line text…"
                        />
                        {content.transcript.length > 1 && (
                            <button type="button" onClick={() => removeTranscriptLine(i)} className="text-xs text-bad shrink-0">
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

            {/* Evaluation axes */}
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Evaluation axes</span>
                    <button type="button" onClick={addAxis} className="text-xs text-ink-3 hover:text-ink">
                        + Add axis
                    </button>
                </div>
                {content.evaluation_axes.map((ax: EvaluationAxis, i: number) => (
                    <div key={i} className="flex items-start gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={ax.name}
                            onChange={(e) => updateAxisName(i, e.target.value)}
                            placeholder="Axis name"
                        />
                        <input
                            className={`${inputCls} flex-1`}
                            value={ax.description}
                            onChange={(e) => updateAxisDescription(i, e.target.value)}
                            placeholder="What to assess…"
                        />
                        {content.evaluation_axes.length > 1 && (
                            <button type="button" onClick={() => removeAxis(i)} className="text-xs text-bad mt-2 shrink-0">
                                ×
                            </button>
                        )}
                    </div>
                ))}
            </div>

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
