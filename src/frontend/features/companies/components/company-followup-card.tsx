"use client";

import { useState } from "react";
import { formatDateRu } from "@/features/companies/lib/format";
import { CompanyFollowUpBadge } from "@/features/companies/components/company-followup-badge";

interface CompanyFollowUpCardProps {
    nextActionAt: string | null;
    nextActionNote: string | null;
    submitting?: boolean;
    onSave: (nextActionAt: string | null, nextActionNote: string | null) => void;
}

function toDateInputValue(iso: string | null): string {
    return iso ? iso.slice(0, 10) : "";
}

export function CompanyFollowUpCard({ nextActionAt, nextActionNote, submitting = false, onSave }: CompanyFollowUpCardProps) {
    const [isEditing, setEditing] = useState(false);
    const [draftDate, setDraftDate] = useState(() => toDateInputValue(nextActionAt));
    const [draftNote, setDraftNote] = useState(nextActionNote ?? "");

    const startEditing = () => {
        setDraftDate(toDateInputValue(nextActionAt));
        setDraftNote(nextActionNote ?? "");
        setEditing(true);
    };

    const cancel = () => {
        setDraftDate(toDateInputValue(nextActionAt));
        setDraftNote(nextActionNote ?? "");
        setEditing(false);
    };

    const save = () => {
        const isoDate = draftDate ? new Date(draftDate).toISOString() : null;
        onSave(isoDate, isoDate ? draftNote.trim() : null);
        setEditing(false);
    };

    const clear = () => {
        onSave(null, null);
        setEditing(false);
    };

    return (
        <div className="co-card">
            <div className="co-card-head">
                <span className="eyebrow">СЛЕДУЮЩИЙ КОНТАКТ</span>
                {isEditing ? (
                    <div className="row gap-3">
                        <button className="btn-link" onClick={cancel}>Отмена</button>
                        <button className="btn-link" onClick={save} disabled={submitting || !draftDate}>Сохранить</button>
                    </div>
                ) : (
                    <button className="btn-link" onClick={startEditing}>Изменить</button>
                )}
            </div>

            {isEditing ? (
                <>
                    <label className="co-field-label" htmlFor="co-followup-date">Дата</label>
                    <input
                        id="co-followup-date"
                        type="date"
                        className="field co-date"
                        value={draftDate}
                        onChange={(event) => setDraftDate(event.target.value)}
                        autoFocus
                    />

                    <label className="co-field-label" htmlFor="co-followup-note" style={{ marginTop: 12 }}>Заметка</label>
                    <textarea
                        id="co-followup-note"
                        className="field co-textarea"
                        style={{ minHeight: 80 }}
                        value={draftNote}
                        onChange={(event) => setDraftNote(event.target.value)}
                        placeholder="О чём напомнить, что важно не забыть"
                        maxLength={2000}
                    />

                    {nextActionAt && (
                        <button className="btn-link" style={{ marginTop: 10, color: "var(--heart)" }} onClick={clear} disabled={submitting}>
                            Убрать напоминание
                        </button>
                    )}
                </>
            ) : nextActionAt ? (
                <div className="row gap-3" style={{ alignItems: "flex-start" }}>
                    <div style={{ flex: 1 }}>
                        <p className="co-desc">{formatDateRu(nextActionAt)}</p>
                        {nextActionNote && <p className="small" style={{ color: "var(--ink-3)", marginTop: 4 }}>{nextActionNote}</p>}
                    </div>
                    <CompanyFollowUpBadge nextActionAt={nextActionAt} />
                </div>
            ) : (
                <div className="co-desc-empty">
                    <span>Запланируйте следующий контакт с этой компанией, и мы напомним, когда придёт время.</span>
                    <button className="btn btn-soft" onClick={startEditing}>Запланировать</button>
                </div>
            )}
        </div>
    );
}
