"use client";

import { WrittenAnswerContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: WrittenAnswerContent;
    onChange: (c: WrittenAnswerContent) => void;
}

export function WrittenAnswerEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Prompt (writing task)</span>
                <textarea rows={3} className={inputCls} value={content.prompt}
                    onChange={(e) => onChange({ ...content, prompt: e.target.value })}
                    placeholder="Write a cold call opening script for a SaaS product..." />
            </label>

            <label className="block">
                <span className={labelCls}>Context</span>
                <input className={inputCls} value={content.context}
                    onChange={(e) => onChange({ ...content, context: e.target.value })}
                    placeholder="B2B software sales, target: IT managers, focus: pain points" />
            </label>

            <div className="grid grid-cols-2 gap-3">
                <label className="block">
                    <span className={labelCls}>Min Length (chars)</span>
                    <input
                        type="number"
                        className={inputCls}
                        value={content.minLength}
                        onChange={(e) => onChange({ ...content, minLength: parseInt(e.target.value) || 0 })}
                        min={0}
                    />
                </label>
                <label className="block">
                    <span className={labelCls}>Max Length (chars)</span>
                    <input
                        type="number"
                        className={inputCls}
                        value={content.maxLength}
                        onChange={(e) => onChange({ ...content, maxLength: parseInt(e.target.value) || 1000 })}
                        min={1}
                    />
                </label>
            </div>

            <label className="block">
                <span className={labelCls}>AI Evaluation Prompt</span>
                <textarea rows={4} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Evaluate clarity, professionalism, and whether the value proposition is compelling..." />
            </label>
        </div>
    );
}
