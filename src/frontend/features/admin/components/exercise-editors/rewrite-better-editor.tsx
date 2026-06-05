"use client";

import { RewriteBetterContent, inputCls, labelCls, textareaCls } from "./types";

interface Props {
    content: RewriteBetterContent;
    onChange: (c: RewriteBetterContent) => void;
}

export function RewriteBetterEditor({ content, onChange }: Props) {
    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Original Text (to be rewritten)</span>
                <textarea rows={3} className={inputCls} value={content.originalText}
                    onChange={(e) => onChange({ ...content, originalText: e.target.value })}
                    placeholder="Buy now or regret it forever!" />
            </label>

            <label className="block">
                <span className={labelCls}>Context</span>
                <input className={inputCls} value={content.context}
                    onChange={(e) => onChange({ ...content, context: e.target.value })}
                    placeholder="Sales email opener, cold call script, etc." />
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
                        onChange={(e) => onChange({ ...content, maxLength: parseInt(e.target.value) || 500 })}
                        min={1}
                    />
                </label>
            </div>

            <label className="block">
                <span className={labelCls}>AI Evaluation Prompt</span>
                <textarea rows={4} className={textareaCls} value={content.aiPrompt}
                    onChange={(e) => onChange({ ...content, aiPrompt: e.target.value })}
                    placeholder="Evaluate if the rewrite is professional, maintains the intent, and avoids pushy language..." />
            </label>
        </div>
    );
}
