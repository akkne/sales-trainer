"use client";

import { useState } from "react";

interface CompanyDescriptionCardProps {
    description: string;
    submitting?: boolean;
    onSave: (description: string) => void;
}

const HELPER_TEXT =
    "Добавьте описание: кто эта компания, что продаёт, кто ЛПР, боли и контекст. Это описание получит ИИ‑собеседник во время тренировки.";

export function CompanyDescriptionCard({ description, submitting = false, onSave }: CompanyDescriptionCardProps) {
    const [isEditing, setEditing] = useState(false);
    const [draft, setDraft] = useState(description);

    const startEditing = () => {
        setDraft(description);
        setEditing(true);
    };

    const cancel = () => {
        setDraft(description);
        setEditing(false);
    };

    const save = () => {
        onSave(draft.trim());
        setEditing(false);
    };

    return (
        <div className="co-card">
            <div className="co-card-head">
                <span className="eyebrow">ОПИСАНИЕ КОМПАНИИ</span>
                {isEditing ? (
                    <div className="row gap-3">
                        <button className="btn-link" onClick={cancel}>Отмена</button>
                        <button className="btn-link" onClick={save} disabled={submitting}>Сохранить</button>
                    </div>
                ) : (
                    <button className="btn-link" onClick={startEditing}>Изменить</button>
                )}
            </div>

            {isEditing ? (
                <>
                    <textarea
                        className="field co-textarea"
                        value={draft}
                        onChange={(event) => setDraft(event.target.value)}
                        placeholder={HELPER_TEXT}
                        maxLength={8000}
                        autoFocus
                    />
                    <p style={{ fontSize: 11.5, color: "var(--ink-4)", margin: "6px 0 0" }}>
                        Это описание ИИ использует как роль собеседника на тренировочных звонках.
                    </p>
                </>
            ) : description ? (
                <p className="co-desc">{description}</p>
            ) : (
                <div className="co-desc-empty">
                    <span>{HELPER_TEXT}</span>
                    <button className="btn btn-soft" onClick={startEditing}>Добавить описание</button>
                </div>
            )}
        </div>
    );
}
