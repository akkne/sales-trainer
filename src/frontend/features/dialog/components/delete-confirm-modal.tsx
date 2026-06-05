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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
            <div className="bg-surface border border-line rounded-2xl max-w-sm w-full" style={{ boxShadow: "var(--sh-3)" }}>
                {/* Header */}
                <div className="p-5 border-b border-line flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <Icon name="delete" size="md" className="text-bad" />
                        <h3 className="text-lg font-bold text-ink">Удалить чат?</h3>
                    </div>
                    <button
                        onClick={onCancel}
                        className="p-2 rounded-full hover:bg-bg-2 transition-colors"
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" className="text-ink-3" />
                    </button>
                </div>

                {/* Content */}
                <div className="p-5">
                    <p className="text-ink-3 text-sm">
                        Вы точно хотите удалить этот чат? Это действие нельзя отменить.
                    </p>
                </div>

                {/* Footer */}
                <div className="p-5 border-t border-line flex gap-3">
                    <button
                        onClick={onCancel}
                        className="flex-1 py-2.5 px-4 bg-surface-2 text-ink font-semibold rounded-full hover:bg-bg-2 transition-colors"
                    >
                        Отмена
                    </button>
                    <button
                        onClick={onConfirm}
                        className="flex-1 py-2.5 px-4 bg-bad text-white font-semibold rounded-full active:translate-y-px transition-colors flex items-center justify-center gap-2"
                        style={{ boxShadow: "var(--sh-2)" }}
                    >
                        <Icon name="delete" size="sm" />
                        Удалить
                    </button>
                </div>
            </div>
        </div>
    );
}
