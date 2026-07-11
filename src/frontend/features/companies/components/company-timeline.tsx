"use client";

import { useMemo, useState } from "react";
import type { PracticeCall } from "@/features/companies/hooks/use-practice-calls";
import type { CallLogEntry, CallLogPayload } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";
import { mergeTimeline, filterTimeline, type TimelineFilter } from "@/features/companies/lib/timeline";
import { CallLogForm } from "@/features/companies/components/call-log-form";
import { TimelinePracticeItem } from "@/features/companies/components/timeline-practice-item";
import { TimelineReallogItem } from "@/features/companies/components/timeline-reallog-item";

interface CompanyTimelineProps {
    companyId: string;
    practiceCalls: PracticeCall[];
    logs: CallLogEntry[];
    contacts?: CompanyContact[];
    addingLog: boolean;
    addLogSubmitting?: boolean;
    onStartAddLog: () => void;
    onCancelAddLog: () => void;
    onAddLog: (payload: CallLogPayload) => unknown;
    onEditLog: (log: CallLogEntry) => void;
    onDeleteLog: (log: CallLogEntry) => void;
}

const SEGMENTS: { key: TimelineFilter; label: string }[] = [
    { key: "all", label: "Все" },
    { key: "practice", label: "Тренировки" },
    { key: "reallog", label: "Звонки" },
];

export function CompanyTimeline({
    companyId,
    practiceCalls,
    logs,
    contacts = [],
    addingLog,
    addLogSubmitting = false,
    onStartAddLog,
    onCancelAddLog,
    onAddLog,
    onEditLog,
    onDeleteLog,
}: CompanyTimelineProps) {
    const [filter, setFilter] = useState<TimelineFilter>("all");

    const entries = useMemo(
        () => filterTimeline(mergeTimeline(practiceCalls, logs), filter),
        [practiceCalls, logs, filter]
    );

    const contactPositionById = useMemo(
        () => new Map(contacts.map((contact) => [contact.id, contact.position])),
        [contacts]
    );

    const canAddLog = filter !== "practice";

    return (
        <div className="co-card co-timeline">
            <div className="co-card-head">
                <span className="eyebrow">ИСТОРИЯ</span>
                <div className="co-seg" role="tablist" aria-label="Фильтр истории">
                    {SEGMENTS.map((segment) => (
                        <button
                            key={segment.key}
                            role="tab"
                            aria-selected={filter === segment.key}
                            className={filter === segment.key ? "active" : ""}
                            onClick={() => setFilter(segment.key)}
                        >
                            {segment.label}
                        </button>
                    ))}
                </div>
            </div>

            {canAddLog && (
                addingLog ? (
                    <CallLogForm
                        companyId={companyId}
                        contacts={contacts}
                        submitting={addLogSubmitting}
                        onSubmit={onAddLog}
                        onCancel={onCancelAddLog}
                    />
                ) : (
                    <button className="btn btn-soft co-add-log" onClick={onStartAddLog}>
                        + Записать звонок
                    </button>
                )
            )}

            {entries.length > 0 ? (
                <div>
                    {entries.map((entry) =>
                        entry.kind === "practice" ? (
                            <TimelinePracticeItem key={`practice-${entry.practiceCall.id}`} practiceCall={entry.practiceCall} />
                        ) : (
                            <TimelineReallogItem
                                key={`reallog-${entry.log.id}`}
                                log={entry.log}
                                contactPosition={
                                    entry.log.contactId ? contactPositionById.get(entry.log.contactId) : undefined
                                }
                                onEdit={() => onEditLog(entry.log)}
                                onDelete={() => onDeleteLog(entry.log)}
                            />
                        )
                    )}
                </div>
            ) : (
                <div className="empty" style={{ padding: "32px 20px" }}>
                    <p className="small">Здесь появятся ваши тренировки и записи о реальных звонках</p>
                </div>
            )}
        </div>
    );
}
