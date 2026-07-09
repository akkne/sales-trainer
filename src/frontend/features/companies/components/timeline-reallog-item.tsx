"use client";

import { Icon } from "@/shared/components/icon";
import type { CallLogEntry } from "@/features/companies/hooks/use-company-logs";
import { formatCalendarDateRu } from "@/features/companies/lib/format";

interface TimelineReallogItemProps {
    log: CallLogEntry;
    onEdit: () => void;
    onDelete: () => void;
}

export function TimelineReallogItem({ log, onEdit, onDelete }: TimelineReallogItemProps) {
    return (
        <div className="co-tl-item reallog">
            <div className="co-tl-node" aria-hidden="true" />
            <div className="co-tl-card">
                <div className="co-tl-top">
                    <span className="co-pill-real">Реальный звонок</span>
                    <span className="co-tl-time">{formatCalendarDateRu(log.occurredAt)}</span>
                    <button className="icon-btn" onClick={onEdit} aria-label="Редактировать запись">
                        <Icon name="edit" size="sm" />
                    </button>
                    <button className="icon-btn" onClick={onDelete} aria-label="Удалить запись">
                        <Icon name="delete" size="sm" />
                    </button>
                </div>

                {log.contactName && (
                    <div className="co-log-field">
                        <div className="co-log-field-label">С кем говорил</div>
                        <div className="co-log-field-value">{log.contactName}</div>
                    </div>
                )}
                {log.subject && (
                    <div className="co-log-field">
                        <div className="co-log-field-label">О чём был разговор</div>
                        <div className="co-log-field-value">{log.subject}</div>
                    </div>
                )}
                {log.outcome && (
                    <div className="co-log-field outcome">
                        <div className="co-log-field-label">К чему пришли</div>
                        <div className="co-log-field-value">{log.outcome}</div>
                    </div>
                )}
            </div>
        </div>
    );
}
