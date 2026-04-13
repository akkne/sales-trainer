"use client";

import { RateCallContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: RateCallContent;
    onChange: (c: RateCallContent) => void;
}

let nextCriteriaId = 100;

export function RateCallEditor({ content, onChange }: Props) {
    function addTranscriptLine() {
        onChange({
            ...content,
            transcript: [...content.transcript, { speaker: "", text: "" }]
        });
    }

    function removeTranscriptLine(index: number) {
        onChange({
            ...content,
            transcript: content.transcript.filter((_, i) => i !== index)
        });
    }

    function updateTranscriptLine(index: number, field: "speaker" | "text", value: string) {
        const transcript = [...content.transcript];
        transcript[index] = { ...transcript[index], [field]: value };
        onChange({ ...content, transcript });
    }

    function addCriteria() {
        const id = String(nextCriteriaId++);
        onChange({
            ...content,
            criteria: [...content.criteria, { id, name: "", description: "" }]
        });
    }

    function removeCriteria(index: number) {
        onChange({
            ...content,
            criteria: content.criteria.filter((_, i) => i !== index)
        });
    }

    function updateCriteria(index: number, field: "name" | "description", value: string) {
        const criteria = [...content.criteria];
        criteria[index] = { ...criteria[index], [field]: value };
        onChange({ ...content, criteria });
    }

    return (
        <div className="space-y-3">
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Call Transcript</span>
                    <button
                        type="button"
                        onClick={addTranscriptLine}
                        className="text-xs text-on-surface-variant hover:text-on-surface"
                    >
                        + Add line
                    </button>
                </div>
                {content.transcript.map((line, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className="w-24 border border-outline-variant rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary bg-surface"
                            value={line.speaker}
                            onChange={(e) => updateTranscriptLine(i, "speaker", e.target.value)}
                            placeholder="Speaker"
                        />
                        <input
                            className="flex-1 border border-outline-variant rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary bg-surface"
                            value={line.text}
                            onChange={(e) => updateTranscriptLine(i, "text", e.target.value)}
                            placeholder="What they said..."
                        />
                        {content.transcript.length > 1 && (
                            <button
                                type="button"
                                onClick={() => removeTranscriptLine(i)}
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
                    <span className={labelCls}>Rating Criteria (user rates 1-5 on each)</span>
                    <button
                        type="button"
                        onClick={addCriteria}
                        className="text-xs text-on-surface-variant hover:text-on-surface"
                    >
                        + Add criterion
                    </button>
                </div>
                {content.criteria.map((criterion, i) => (
                    <div key={criterion.id} className="mt-2 p-2 bg-surface-container-low rounded">
                        <div className="flex items-center gap-2">
                            <input
                                className="w-32 border border-outline-variant rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary bg-surface"
                                value={criterion.name}
                                onChange={(e) => updateCriteria(i, "name", e.target.value)}
                                placeholder="Criterion name"
                            />
                            <input
                                className="flex-1 border border-outline-variant rounded px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary bg-surface"
                                value={criterion.description}
                                onChange={(e) => updateCriteria(i, "description", e.target.value)}
                                placeholder="Description of what to evaluate"
                            />
                            {content.criteria.length > 1 && (
                                <button
                                    type="button"
                                    onClick={() => removeCriteria(i)}
                                    className="text-xs text-error hover:text-error/80"
                                >
                                    ×
                                </button>
                            )}
                        </div>
                    </div>
                ))}
            </div>

            <label className="block">
                <span className={labelCls}>AI Evaluation Prompt</span>
                <textarea rows={4} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Compare user ratings with the actual call quality. Provide feedback on rating accuracy..." />
            </label>
        </div>
    );
}
