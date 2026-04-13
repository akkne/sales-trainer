"use client";

import { OpenQuestionContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: OpenQuestionContent;
    onChange: (c: OpenQuestionContent) => void;
}

export function OpenQuestionEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Question text</span>
                <input className={inputCls} value={content.question}
                    onChange={(e) => onChange({ ...content, question: e.target.value })} />
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
