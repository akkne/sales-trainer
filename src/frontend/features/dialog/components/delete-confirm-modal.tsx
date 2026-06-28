"use client";

import { useEffect } from "react";
import { Icon } from "@/shared/components/icon";

interface DeleteConfirmModalProps {
    onConfirm: () => void;
    onCancel: () => void;
}

export function DeleteConfirmModal({ onConfirm, onCancel }: DeleteConfirmModalProps) {
    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key === "Escape") {
                onCancel();
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onCancel]);

    return (
        <div className="modal-overlay" onClick={onCancel}>
            <div className="modal fade-up" style={{ maxWidth: 400 }} onClick={(event) => event.stopPropagation()}>
                <div className="modal-head">
                    <div className="row gap-3">
                        <span className="itile heart" style={{ width: 40, height: 40 }}>
                            <Icon name="delete" size="md" />
                        </span>
                        <h3 className="h3">Delete chat?</h3>
                    </div>
                    <button className="icon-btn" onClick={onCancel} aria-label="Close">
                        <Icon name="close" size="md" />
                    </button>
                </div>

                <div className="modal-body">
                    <p className="body" style={{ margin: 0 }}>
                        Are you sure you want to delete this chat? This action cannot be undone.
                    </p>
                </div>

                <div className="modal-foot row gap-3">
                    <button className="btn btn-outline grow" onClick={onCancel}>
                        Cancel
                    </button>
                    <button className="btn btn-danger grow" onClick={onConfirm}>
                        <Icon name="delete" size="sm" />
                        Delete
                    </button>
                </div>
            </div>
        </div>
    );
}
