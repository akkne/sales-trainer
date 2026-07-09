"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/shared/components/icon";

interface CompanyModalProps {
    /** When set, the modal opens in edit mode pre-filled with these values. */
    initial?: { name: string; description: string };
    submitting?: boolean;
    onSubmit: (values: { name: string; description: string }) => void;
    onClose: () => void;
}

/** Create / edit company modal (§5.1 of the design spec). */
export function CompanyModal({ initial, submitting = false, onSubmit, onClose }: CompanyModalProps) {
    const isEdit = !!initial;
    const [name, setName] = useState(initial?.name ?? "");
    const [description, setDescription] = useState(initial?.description ?? "");

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") onClose();
        };
        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    const canSubmit = name.trim().length > 0 && !submitting;

    const handleSubmit = () => {
        if (!canSubmit) return;
        onSubmit({ name: name.trim(), description: description.trim() });
    };

    return (
        <div
            className="modal-overlay"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-labelledby="co-modal-title"
        >
            <div className="modal fade-up" onClick={(event) => event.stopPropagation()}>
                <div className="modal-head">
                    <h3 id="co-modal-title" className="h3">
                        {isEdit ? "Редактировать компанию" : "Новая компания"}
                    </h3>
                    <button className="icon-btn" onClick={onClose} aria-label="Закрыть">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    <label className="co-field-label" htmlFor="co-modal-name">Название</label>
                    <input
                        id="co-modal-name"
                        className="field"
                        style={{ marginBottom: 16 }}
                        value={name}
                        onChange={(event) => setName(event.target.value)}
                        placeholder="Напр. ООО «Ромашка»"
                        maxLength={120}
                        autoFocus
                        required
                    />

                    <label className="co-field-label" htmlFor="co-modal-desc">Описание</label>
                    <textarea
                        id="co-modal-desc"
                        className="field co-textarea"
                        value={description}
                        onChange={(event) => setDescription(event.target.value)}
                        placeholder="Добавьте описание: кто эта компания, что продаёт, кто ЛПР, боли и контекст. Это описание получит ИИ‑собеседник во время тренировки."
                    />
                    <p style={{ fontSize: 11.5, color: "var(--ink-4)", margin: "6px 0 0" }}>
                        Можно заполнить позже на странице компании.
                    </p>
                </div>

                <div className="modal-foot row gap-3">
                    <button className="btn btn-ghost" onClick={onClose}>Отмена</button>
                    <button className="btn btn-primary" onClick={handleSubmit} disabled={!canSubmit}>
                        {isEdit ? "Сохранить" : "Создать"}
                    </button>
                </div>
            </div>
        </div>
    );
}
