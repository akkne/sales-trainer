"use client";

import { useState } from "react";
import type { CallLogEntry, CallLogPayload } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";

function todayIso(): string {
    return new Date().toISOString().slice(0, 10);
}

interface CallLogFormProps {
    initial?: CallLogEntry;
    contacts?: CompanyContact[];
    submitting?: boolean;
    onSubmit: (payload: CallLogPayload) => void;
    onCancel: () => void;
}

export function CallLogForm({ initial, contacts = [], submitting = false, onSubmit, onCancel }: CallLogFormProps) {
    const [contactName, setContactName] = useState(initial?.contactName ?? "");
    const [contactId, setContactId] = useState<string | null>(initial?.contactId ?? null);
    const [subject, setSubject] = useState(initial?.subject ?? "");
    const [outcome, setOutcome] = useState(initial?.outcome ?? "");
    const [occurredAt, setOccurredAt] = useState(
        initial ? initial.occurredAt.slice(0, 10) : todayIso()
    );

    const canSubmit = contactName.trim().length > 0 && !submitting;

    const handlePickContact = (contact: CompanyContact) => {
        setContactName(contact.name);
        setContactId(contact.id);
    };

    const handleContactNameChange = (value: string) => {
        setContactName(value);
        setContactId(null);
    };

    const handleSubmit = () => {
        if (!canSubmit) return;
        onSubmit({
            contactName: contactName.trim(),
            subject: subject.trim(),
            outcome: outcome.trim(),
            occurredAt: new Date(occurredAt).toISOString(),
            contactId,
        });
    };

    return (
        <div className="co-log-form">
            <label className="co-field-label" htmlFor="co-log-contact">С кем говорил</label>
            {contacts.length > 0 && (
                <div className="co-contact-chips" role="group" aria-label="Существующие контакты">
                    {contacts.map((contact) => (
                        <button
                            key={contact.id}
                            type="button"
                            className={contactId === contact.id ? "chip-tag active" : "chip-tag"}
                            onClick={() => handlePickContact(contact)}
                        >
                            {contact.name}
                        </button>
                    ))}
                </div>
            )}
            <input
                id="co-log-contact"
                className="field"
                value={contactName}
                onChange={(event) => handleContactNameChange(event.target.value)}
                placeholder="Имя и должность, напр. Иван, руководитель отдела закупок"
                maxLength={200}
                autoFocus
                required
            />

            <label className="co-field-label" htmlFor="co-log-subject">О чём был разговор</label>
            <textarea
                id="co-log-subject"
                className="field co-textarea"
                style={{ minHeight: 88 }}
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                placeholder="Кратко о ходе разговора"
                maxLength={4000}
            />

            <label className="co-field-label" htmlFor="co-log-outcome">К чему пришли</label>
            <textarea
                id="co-log-outcome"
                className="field co-textarea"
                style={{ minHeight: 72 }}
                value={outcome}
                onChange={(event) => setOutcome(event.target.value)}
                placeholder="Договорённости, следующий шаг"
                maxLength={4000}
            />

            <label className="co-field-label" htmlFor="co-log-date">Дата</label>
            <input
                id="co-log-date"
                type="date"
                className="field co-date"
                value={occurredAt}
                onChange={(event) => setOccurredAt(event.target.value)}
            />

            <div className="co-log-form-foot">
                <button className="btn btn-ghost" onClick={onCancel}>Отмена</button>
                <button className="btn btn-primary" onClick={handleSubmit} disabled={!canSubmit}>
                    Сохранить запись
                </button>
            </div>
        </div>
    );
}
