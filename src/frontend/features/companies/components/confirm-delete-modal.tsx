"use client";

import { useEffect } from "react";
import { Icon } from "@/shared/components/icon";

interface ConfirmDeleteModalProps {
    title: string;
    body: string;
    submitting?: boolean;
    onConfirm: () => void;
    onClose: () => void;
}

export function ConfirmDeleteModal({ title, body, submitting = false, onConfirm, onClose }: ConfirmDeleteModalProps) {
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
            aria-labelledby="co-confirm-title"
        >
            <div
                className="modal fade-up"
                style={{ maxWidth: 440 }}
                onClick={(event) => event.stopPropagation()}
            >
                <div className="modal-head">
                    <h3 id="co-confirm-title" className="h3">{title}</h3>
                    <button className="icon-btn" onClick={onClose} aria-label="Закрыть">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    <p className="small">{body}</p>
                </div>

                <div className="modal-foot row gap-3">
                    <button className="btn btn-ghost" onClick={onClose}>Отмена</button>
                    <button className="btn btn-danger" onClick={onConfirm} disabled={submitting}>
                        Удалить
                    </button>
                </div>
            </div>
        </div>
    );
}
