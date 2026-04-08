"use client";

import { useEffect } from "react";
import { Icon } from "@/components/ui/Icon";

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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-inverse-surface/50 p-4">
            <div className="bg-surface-container-lowest rounded-2xl max-w-sm w-full shadow-xl">
                {/* Header */}
                <div className="p-5 border-b border-outline-variant flex items-center justify-between">
                    <div className="flex items-center gap-2">
                        <Icon name="delete" size="md" className="text-error" />
                        <h3 className="text-lg font-headline font-bold text-on-surface">Удалить чат?</h3>
                    </div>
                    <button
                        onClick={onCancel}
                        className="p-2 rounded-full hover:bg-surface-container tonal-transition"
                        aria-label="Закрыть"
                    >
                        <Icon name="close" size="md" className="text-on-surface-variant" />
                    </button>
                </div>

                {/* Content */}
                <div className="p-5">
                    <p className="text-on-surface-variant text-sm">
                        Вы точно хотите удалить этот чат? Это действие нельзя отменить.
                    </p>
                </div>

                {/* Footer */}
                <div className="p-5 border-t border-outline-variant flex gap-3">
                    <button
                        onClick={onCancel}
                        className="flex-1 py-2.5 px-4 bg-surface-container-high text-on-surface font-semibold rounded-full hover:bg-surface-container-highest tonal-transition"
                    >
                        Отмена
                    </button>
                    <button
                        onClick={onConfirm}
                        className="flex-1 py-2.5 px-4 bg-error text-on-error font-semibold rounded-full shadow-[0_4px_0_var(--color-red-shadow)] active:shadow-none active:translate-y-1 tonal-transition flex items-center justify-center gap-2"
                    >
                        <Icon name="delete" size="sm" />
                        Удалить
                    </button>
                </div>
            </div>
        </div>
    );
}
