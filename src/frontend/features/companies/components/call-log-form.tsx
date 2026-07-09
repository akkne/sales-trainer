"use client";

import { useState } from "react";
import type { CallLogEntry, CallLogPayload } from "@/features/companies/hooks/use-company-logs";

function todayIso(): string {
    return new Date().toISOString().slice(0, 10);
}

interface CallLogFormProps {
    initial?: CallLogEntry;
    submitting?: boolean;
    onSubmit: (payload: CallLogPayload) => void;
    onCancel: () => void;
}

/** 3-field + date form for adding/editing a real-call log entry (§3.5 of the design spec). */
export function CallLogForm({ initial, submitting = false, onSubmit, onCancel }: CallLogFormProps) {
    const [contactName, setContactName] = useState(initial?.contactName ?? "");
    const [subject, setSubject] = useState(initial?.subject ?? "");
    const [outcome, setOutcome] = useState(initial?.outcome ?? "");
    const [occurredAt, setOccurredAt] = useState(
        initial ? initial.occurredAt.slice(0, 10) : todayIso()
    );

    const canSubmit = contactName.trim().length > 0 && !submitting;

    const handleSubmit = () => {
        if (!canSubmit) return;
        onSubmit({
            contactName: contactName.trim(),
            subject: subject.trim(),
            outcome: outcome.trim(),
            occurredAt: new Date(occurredAt).toISOString(),
        });
    };

    return (
        <div className="co-log-form">
            <label className="co-field-label" htmlFor="co-log-contact">С кем говорил</label>
            <input
                id="co-log-contact"
                className="field"
                value={contactName}
                onChange={(event) => setContactName(event.target.value)}
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
            />

            <label className="co-field-label" htmlFor="co-log-outcome">К чему пришли</label>
            <textarea
                id="co-log-outcome"
                className="field co-textarea"
                style={{ minHeight: 72 }}
                value={outcome}
                onChange={(event) => setOutcome(event.target.value)}
                placeholder="Договорённости, следующий шаг"
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
