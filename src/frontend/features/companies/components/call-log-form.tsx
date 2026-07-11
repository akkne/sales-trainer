"use client";

import { useState } from "react";
import type { CallLogEntry, CallLogPayload } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";
import { useParseCallLog } from "@/features/companies/hooks/use-parse-call-log";
import { useVoiceMemoRecorder } from "@/features/companies/hooks/use-voice-memo-recorder";
import { Icon } from "@/shared/components/icon";
import { ApiError } from "@/shared/api/api-client";

function todayIso(): string {
    return new Date().toISOString().slice(0, 10);
}

interface CallLogFormProps {
    companyId: string;
    initial?: CallLogEntry;
    contacts?: CompanyContact[];
    submitting?: boolean;
    onSubmit: (payload: CallLogPayload) => unknown;
    onCancel: () => void;
}

export function CallLogForm({ companyId, initial, contacts = [], submitting = false, onSubmit, onCancel }: CallLogFormProps) {
    const [contactName, setContactName] = useState(initial?.contactName ?? "");
    const [contactId, setContactId] = useState<string | null>(initial?.contactId ?? null);
    const [subject, setSubject] = useState(initial?.subject ?? "");
    const [outcome, setOutcome] = useState(initial?.outcome ?? "");
    const [occurredAt, setOccurredAt] = useState(
        initial ? initial.occurredAt.slice(0, 10) : todayIso()
    );

    // Paste-notes mode (39.13): user pastes raw notes/transcript, AI prefills the fields above
    // for review before saving. Only offered when creating a new entry (not while editing).
    const [isPasteMode, setPasteMode] = useState(false);
    const [rawNotes, setRawNotes] = useState("");
    const parseCallLog = useParseCallLog(companyId);

    // Voice memo (39.15): record → transcribe → land the transcript in the raw-notes
    // textarea above, so it can feed the same "Распознать" AI parse as pasted text.
    const voiceMemo = useVoiceMemoRecorder({
        onTranscript: (text) => {
            setRawNotes((current) => (current.trim() ? `${current}\n${text}` : text));
        },
    });

    const canSubmit = contactName.trim().length > 0 && !submitting;

    const handleParseNotes = () => {
        if (!rawNotes.trim()) return;
        parseCallLog.mutate(rawNotes, {
            onSuccess: (parsed) => {
                if (parsed.contactName) setContactName(parsed.contactName);
                setSubject(parsed.subject);
                setOutcome(parsed.outcome);
                if (parsed.occurredAt) setOccurredAt(parsed.occurredAt.slice(0, 10));
                setPasteMode(false);
            },
            // Graceful fallback: on AI failure, stay in paste mode with the toast/inline error
            // shown below and the manual fields untouched — the form remains fully usable.
        });
    };

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
        Promise.resolve(
            onSubmit({
                contactName: contactName.trim(),
                subject: subject.trim(),
                outcome: outcome.trim(),
                occurredAt: new Date(occurredAt).toISOString(),
                contactId,
            })
        ).catch((error: unknown) => {
            // The picked contact was deleted by someone else between load and submit — the API
            // rejects the stale contactId with 400. Clear it so retrying re-submits as free text
            // instead of repeating the same failing request.
            if (error instanceof ApiError && error.status === 400) {
                setContactId(null);
            }
        });
    };

    return (
        <div className="co-log-form">
            {!initial && (
                isPasteMode ? (
                    <div className="co-paste-notes">
                        <div className="co-paste-notes-head">
                            <label className="co-field-label" htmlFor="co-log-raw-notes">Вставьте заметки или расшифровку звонка</label>
                            {voiceMemo.isSupported && (
                                <button
                                    type="button"
                                    className={
                                        "co-mic-btn" +
                                        (voiceMemo.state === "recording" ? " co-mic-btn--recording" : "")
                                    }
                                    onClick={voiceMemo.state === "recording" ? voiceMemo.stopRecording : voiceMemo.startRecording}
                                    disabled={voiceMemo.state === "requesting-permission" || voiceMemo.state === "transcribing"}
                                    aria-label={voiceMemo.state === "recording" ? "Остановить запись" : "Наговорить заметку"}
                                >
                                    {voiceMemo.state === "transcribing" ? (
                                        <span className="co-mic-spinner" aria-hidden="true" />
                                    ) : (
                                        <Icon name="mic" size="sm" />
                                    )}
                                    <span>
                                        {voiceMemo.state === "requesting-permission" && "Запрос доступа…"}
                                        {voiceMemo.state === "recording" && "Остановить"}
                                        {voiceMemo.state === "transcribing" && "Распознаём…"}
                                        {(voiceMemo.state === "idle" || voiceMemo.state === "error") && "Наговорить"}
                                    </span>
                                </button>
                            )}
                        </div>
                        <textarea
                            id="co-log-raw-notes"
                            className="field co-textarea"
                            style={{ minHeight: 120 }}
                            value={rawNotes}
                            onChange={(event) => setRawNotes(event.target.value)}
                            placeholder="Вставьте текст — AI распознает с кем говорили, о чём и к чему пришли"
                            maxLength={16000}
                            autoFocus
                        />
                        {voiceMemo.error && (
                            <p className="small" style={{ color: "var(--heart)" }}>
                                {voiceMemo.error}. Можно вставить или напечатать текст вручную.
                            </p>
                        )}
                        {parseCallLog.isError && (
                            <p className="small" style={{ color: "var(--heart)" }}>
                                Не удалось распознать заметки: {parseCallLog.error.message}. Можно заполнить поля вручную.
                            </p>
                        )}
                        <div className="co-log-form-foot">
                            <button className="btn btn-ghost" onClick={() => setPasteMode(false)}>
                                Заполнить вручную
                            </button>
                            <button
                                className="btn btn-primary"
                                onClick={handleParseNotes}
                                disabled={!rawNotes.trim() || parseCallLog.isPending}
                            >
                                {parseCallLog.isPending ? "Распознаём…" : "Распознать"}
                            </button>
                        </div>
                    </div>
                ) : (
                    <button type="button" className="btn-link" onClick={() => setPasteMode(true)}>
                        Вставить заметки
                    </button>
                )
            )}

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
                autoFocus={!isPasteMode}
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
