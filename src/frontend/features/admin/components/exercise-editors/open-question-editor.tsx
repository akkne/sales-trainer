"use client";

import { FreeTextContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: FreeTextContent;
    onChange: (c: FreeTextContent) => void;
}

export function OpenQuestionEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Prompt text</span>
                <input className={inputCls} value={content.prompt}
                    onChange={(e) => onChange({ ...content, prompt: e.target.value })} />
            </label>
            <label className="block">
                <span className={labelCls}>Context (optional)</span>
                <textarea rows={3} className={textareaCls} value={content.context}
                    onChange={(e) => onChange({ ...content, context: e.target.value })}
                    placeholder="Additional context for the question..." />
            </label>
            <label className="block">
                <span className={labelCls}>AI evaluation prompt (criteria)</span>
                <textarea rows={6} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Rate the answer based on whether the user mentions..." />
            </label>
        </div>
    );
}
