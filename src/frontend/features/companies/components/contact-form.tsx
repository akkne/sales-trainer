"use client";

import { useState } from "react";
import type { CompanyContact, CompanyContactPayload } from "@/features/companies/hooks/use-company-contacts";

interface ContactFormProps {
    initial?: CompanyContact;
    submitting?: boolean;
    onSubmit: (payload: CompanyContactPayload) => void;
    onCancel: () => void;
}

export function ContactForm({ initial, submitting = false, onSubmit, onCancel }: ContactFormProps) {
    const [name, setName] = useState(initial?.name ?? "");
    const [position, setPosition] = useState(initial?.position ?? "");
    const [notes, setNotes] = useState(initial?.notes ?? "");

    const canSubmit = name.trim().length > 0 && !submitting;

    const handleSubmit = () => {
        if (!canSubmit) return;
        onSubmit({
            name: name.trim(),
            position: position.trim(),
            notes: notes.trim(),
        });
    };

    return (
        <div className="co-contact-form">
            <label className="co-field-label" htmlFor="co-contact-name">Имя</label>
            <input
                id="co-contact-name"
                className="field"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Имя контакта"
                maxLength={200}
                autoFocus
                required
            />

            <label className="co-field-label" htmlFor="co-contact-position">Должность</label>
            <input
                id="co-contact-position"
                className="field"
                value={position}
                onChange={(event) => setPosition(event.target.value)}
                placeholder="Напр. руководитель отдела закупок"
                maxLength={200}
            />

            <label className="co-field-label" htmlFor="co-contact-notes">Заметки</label>
            <textarea
                id="co-contact-notes"
                className="field co-textarea"
                style={{ minHeight: 80 }}
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                placeholder="Что важно помнить об этом человеке"
                maxLength={2000}
            />

            <div className="co-log-form-foot">
                <button className="btn btn-ghost" onClick={onCancel}>Отмена</button>
                <button className="btn btn-primary" onClick={handleSubmit} disabled={!canSubmit}>
                    Сохранить контакт
                </button>
            </div>
        </div>
    );
}
