"use client";

import { TheoryCardContent, inputCls, labelCls, textareaCls } from "./types";
import type { TheoryCardLayout, TheoryDialogueTurn } from "@/features/exercise/types/theory-card";

interface Props {
    content: TheoryCardContent;
    onChange: (c: TheoryCardContent) => void;
}

const LAYOUTS: TheoryCardLayout[] = ["text", "dialogue", "bullets", "quote"];

/** Editor for theory_card exercises (non-graded story cards). Fields depend on `layout`. */
export function TheoryCardEditor({ content, onChange }: Props) {
    function changeLayout(layout: TheoryCardLayout) {
        if (layout === content.layout) return;
        // Reset to a minimal valid shape for the chosen layout.
        switch (layout) {
            case "text": onChange({ layout: "text", title: "", body: "" }); break;
            case "dialogue": onChange({ layout: "dialogue", title: "", turns: [{ side: "them", text: "" }, { side: "me", text: "" }] }); break;
            case "bullets": onChange({ layout: "bullets", title: "", items: [""] }); break;
            case "quote": onChange({ layout: "quote", text: "", author: "" }); break;
        }
    }

    return (
        <div className="space-y-3">
            <label className="block">
                <span className={labelCls}>Layout</span>
                <select
                    className={inputCls}
                    value={content.layout}
                    onChange={(e) => changeLayout(e.target.value as TheoryCardLayout)}
                >
                    {LAYOUTS.map((l) => (
                        <option key={l} value={l}>{l}</option>
                    ))}
                </select>
            </label>

            {content.layout === "text" && (
                <>
                    <label className="block">
                        <span className={labelCls}>Title (optional)</span>
                        <input className={inputCls} value={content.title ?? ""} onChange={(e) => onChange({ ...content, title: e.target.value })} />
                    </label>
                    <label className="block">
                        <span className={labelCls}>Body (one paragraph per line)</span>
                        <textarea rows={6} className={textareaCls} value={content.body} onChange={(e) => onChange({ ...content, body: e.target.value })} />
                    </label>
                </>
            )}

            {content.layout === "dialogue" && (
                <DialogueEditor content={content} onChange={onChange} />
            )}

            {content.layout === "bullets" && (
                <BulletsEditor content={content} onChange={onChange} />
            )}

            {content.layout === "quote" && (
                <>
                    <label className="block">
                        <span className={labelCls}>Quote text</span>
                        <textarea rows={3} className={textareaCls} value={content.text} onChange={(e) => onChange({ ...content, text: e.target.value })} />
                    </label>
                    <label className="block">
                        <span className={labelCls}>Author (optional)</span>
                        <input className={inputCls} value={content.author ?? ""} onChange={(e) => onChange({ ...content, author: e.target.value })} />
                    </label>
                </>
            )}
        </div>
    );
}

function DialogueEditor({ content, onChange }: { content: Extract<TheoryCardContent, { layout: "dialogue" }>; onChange: (c: TheoryCardContent) => void }) {
    const turns = content.turns;

    function update(i: number, patch: Partial<TheoryDialogueTurn>) {
        onChange({ ...content, turns: turns.map((t, idx) => (idx === i ? { ...t, ...patch } : t)) });
    }
    function add() {
        onChange({ ...content, turns: [...turns, { side: "them", text: "" }] });
    }
    function remove(i: number) {
        onChange({ ...content, turns: turns.filter((_, idx) => idx !== i) });
    }

    return (
        <>
            <label className="block">
                <span className={labelCls}>Title (optional)</span>
                <input className={inputCls} value={content.title ?? ""} onChange={(e) => onChange({ ...content, title: e.target.value })} />
            </label>
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Turns (me = seller, them = client)</span>
                    <button type="button" onClick={add} className="text-xs text-ink-3 hover:text-ink">+ Add turn</button>
                </div>
                {turns.map((turn, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <select
                            className={inputCls}
                            style={{ width: 90, flex: "none" }}
                            value={turn.side}
                            onChange={(e) => update(i, { side: e.target.value as "me" | "them" })}
                        >
                            <option value="them">them</option>
                            <option value="me">me</option>
                        </select>
                        <input className={inputCls} value={turn.text} onChange={(e) => update(i, { text: e.target.value })} placeholder="Реплика" />
                        <button type="button" onClick={() => remove(i)} className="text-xs text-bad shrink-0">×</button>
                    </div>
                ))}
            </div>
        </>
    );
}

function BulletsEditor({ content, onChange }: { content: Extract<TheoryCardContent, { layout: "bullets" }>; onChange: (c: TheoryCardContent) => void }) {
    const items = content.items;

    function update(i: number, value: string) {
        onChange({ ...content, items: items.map((it, idx) => (idx === i ? value : it)) });
    }
    function add() {
        onChange({ ...content, items: [...items, ""] });
    }
    function remove(i: number) {
        onChange({ ...content, items: items.filter((_, idx) => idx !== i) });
    }

    return (
        <>
            <label className="block">
                <span className={labelCls}>Title (optional)</span>
                <input className={inputCls} value={content.title ?? ""} onChange={(e) => onChange({ ...content, title: e.target.value })} />
            </label>
            <div>
                <div className="flex items-center justify-between mb-1">
                    <span className={labelCls}>Bullets</span>
                    <button type="button" onClick={add} className="text-xs text-ink-3 hover:text-ink">+ Add bullet</button>
                </div>
                {items.map((item, i) => (
                    <div key={i} className="flex items-center gap-2 mt-1">
                        <input className={inputCls} value={item} onChange={(e) => update(i, e.target.value)} placeholder={`Bullet ${i + 1}`} />
                        <button type="button" onClick={() => remove(i)} className="text-xs text-bad shrink-0">×</button>
                    </div>
                ))}
            </div>
        </>
    );
}
