"use client";

import { AiDialogueContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: AiDialogueContent;
    onChange: (c: AiDialogueContent) => void;
}

export function AiDialogEditor({ content, onChange }: Props) {
    const criteria = content.success_criteria ?? [];

    function addCriterion() {
        onChange({ ...content, success_criteria: [...criteria, ""] });
    }

    function updateCriterion(index: number, value: string) {
        onChange({ ...content, success_criteria: criteria.map((c, i) => i === index ? value : c) });
    }

    function removeCriterion(index: number) {
        onChange({ ...content, success_criteria: criteria.filter((_, i) => i !== index) });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Persona (AI character description)</span>
                <textarea
                    rows={3}
                    className={textareaCls}
                    value={content.persona}
                    onChange={(e) => onChange({ ...content, persona: e.target.value })}
                    placeholder="Sceptical IT director, responds briefly and is in a hurry…"
                />
            </label>

            <label className="block">
                <span className={labelCls}>Scenario</span>
                <input
                    className={inputCls}
                    value={content.scenario}
                    onChange={(e) => onChange({ ...content, scenario: e.target.value })}
                    placeholder="Discovery call with an IT director"
                />
            </label>

            <label className="block">
                <span className={labelCls}>Context (optional background)</span>
                <textarea
                    rows={2}
                    className={textareaCls}
                    value={content.context ?? ""}
                    onChange={(e) => onChange({ ...content, context: e.target.value })}
                />
            </label>

            <label className="block">
                <span className={labelCls}>Max turns</span>
                <input
                    type="number"
                    min={1}
                    className={inputCls}
                    value={content.max_turns ?? 6}
                    onChange={(e) => onChange({ ...content, max_turns: Number(e.target.value) || 6 })}
                />
            </label>

            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Success criteria</span>
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
