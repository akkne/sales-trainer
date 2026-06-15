"use client";

import { RewriteContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: RewriteContent;
    onChange: (c: RewriteContent) => void;
}

export function RewriteBetterEditor({ content, onChange }: Props) {
    const criteria = content.evaluation_criteria ?? [];

    function addCriterion() {
        onChange({ ...content, evaluation_criteria: [...criteria, ""] });
    }

    function updateCriterion(index: number, value: string) {
        onChange({ ...content, evaluation_criteria: criteria.map((c, i) => i === index ? value : c) });
    }

    function removeCriterion(index: number) {
        onChange({ ...content, evaluation_criteria: criteria.filter((_, i) => i !== index) });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Instruction</span>
                <input
                    className={inputCls}
                    value={content.instruction}
                    onChange={(e) => onChange({ ...content, instruction: e.target.value })}
                    placeholder="Rewrite the following to be more effective…"
                />
            </label>

            <label className="block">
                <span className={labelCls}>Original (text to rewrite)</span>
                <textarea
                    rows={3}
                    className={textareaCls}
                    value={content.original}
                    onChange={(e) => onChange({ ...content, original: e.target.value })}
                    placeholder="The original text the learner must improve…"
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Evaluation criteria</span>
                    <button type="button" onClick={addCriterion} className="text-xs text-ink-3 hover:text-ink">
                        + Add criterion
                    </button>
                </div>
                {criteria.map((crit: string, i: number) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input
                            className={inputCls}
                            value={crit}
                            onChange={(e) => updateCriterion(i, e.target.value)}
                            placeholder={`Criterion ${i + 1}`}
                        />
                        <button type="button" onClick={() => removeCriterion(i)} className="text-xs text-bad shrink-0">
                            ×
                        </button>
                    </div>
                ))}
                {criteria.length === 0 && (
                    <p className="text-[10px] text-ink-3 mt-1">No criteria yet — AI grader will use the global prompt only.</p>
                )}
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
