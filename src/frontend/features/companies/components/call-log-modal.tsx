"use client";

import { useEffect } from "react";
import { Icon } from "@/shared/components/icon";
import { CallLogForm } from "@/features/companies/components/call-log-form";
import type { CallLogEntry, CallLogPayload } from "@/features/companies/hooks/use-company-logs";
import type { CompanyContact } from "@/features/companies/hooks/use-company-contacts";

interface CallLogModalProps {
    companyId: string;
    initial?: CallLogEntry;
    contacts?: CompanyContact[];
    submitting?: boolean;
    onSubmit: (payload: CallLogPayload) => void;
    onClose: () => void;
}

export function CallLogModal({ companyId, initial, contacts = [], submitting = false, onSubmit, onClose }: CallLogModalProps) {
    const isEdit = !!initial;

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") onClose();
        };
        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    return (
        <div
            className="modal-overlay"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-labelledby="co-log-modal-title"
        >
            <div className="modal fade-up" onClick={(event) => event.stopPropagation()}>
                <div className="modal-head">
                    <h3 id="co-log-modal-title" className="h3">
                        {isEdit ? "Изменить запись" : "Запись о звонке"}
                    </h3>
                    <button className="icon-btn" onClick={onClose} aria-label="Закрыть">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    <CallLogForm
                        companyId={companyId}
                        initial={initial}
                        contacts={contacts}
                        submitting={submitting}
                        onSubmit={onSubmit}
                        onCancel={onClose}
                    />
                </div>
            </div>
        </div>
    );
}
