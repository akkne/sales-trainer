"use client";

import { AiDialogContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: AiDialogContent;
    onChange: (c: AiDialogContent) => void;
}

export function AiDialogEditor({ content, onChange }: Props) {
    function updatePersona(field: keyof AiDialogContent["persona"], value: string) {
        onChange({
            ...content,
            persona: { ...content.persona, [field]: value }
        });
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Scenario Description</span>
                <textarea rows={2} className={inputCls} value={content.scenario}
                    onChange={(e) => onChange({ ...content, scenario: e.target.value })}
                    placeholder="You are calling a potential B2B client about your SaaS product..." />
            </label>

            <div className="p-3 bg-surface-container-low rounded-md">
                <span className={labelCls + " block mb-2"}>AI Persona</span>
                <div className="grid grid-cols-3 gap-2">
                    <label className="block">
                        <span className="text-[10px] text-on-surface-variant">Name</span>
                        <input className={inputCls} value={content.persona.name}
                            onChange={(e) => updatePersona("name", e.target.value)}
                            placeholder="Sarah" />
                    </label>
                    <label className="block">
                        <span className="text-[10px] text-on-surface-variant">Role</span>
                        <input className={inputCls} value={content.persona.role}
                            onChange={(e) => updatePersona("role", e.target.value)}
                            placeholder="IT Manager" />
                    </label>
                    <label className="block">
                        <span className="text-[10px] text-on-surface-variant">Personality</span>
                        <input className={inputCls} value={content.persona.personality}
                            onChange={(e) => updatePersona("personality", e.target.value)}
                            placeholder="Skeptical but fair" />
                    </label>
                </div>
            </div>

            <label className="block">
                <span className={labelCls}>System Prompt (AI instructions)</span>
                <textarea rows={6} className={textareaCls} value={content.systemPrompt}
                    onChange={(e) => onChange({ ...content, systemPrompt: e.target.value })}
                    placeholder="You are Sarah, an IT Manager at a mid-size company. You are skeptical of sales calls but will engage if the salesperson shows genuine understanding of your challenges..." />
            </label>

            <label className="block">
                <span className={labelCls}>Min Turns for Completion</span>
                <input
                    type="number"
                    className={inputCls + " w-24"}
                    value={content.minTurnsForCompletion}
                    onChange={(e) => onChange({ ...content, minTurnsForCompletion: parseInt(e.target.value) || 4 })}
                    min={1}
                    max={20}
                />
                <span className="text-[10px] text-on-surface-variant ml-2">
                    Minimum conversation turns before completion is possible
                </span>
            </label>

            <label className="block">
                <span className={labelCls}>AI Evaluation Prompt (for grading)</span>
                <textarea rows={4} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Evaluate the salesperson's rapport building, discovery questions, and ability to handle objections..." />
            </label>
        </div>
    );
}
