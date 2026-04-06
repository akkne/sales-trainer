"use client";

import { useEffect } from "react";

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
            <div className="bg-white rounded-2xl max-w-sm w-full">
                <div className="p-4 border-b border-gray-100 flex items-center justify-between">
                    <h3 className="text-lg font-bold text-gray-800">Удалить чат?</h3>
                    <button
                        onClick={onCancel}
                        className="text-2xl text-gray-400 hover:text-gray-600 transition-colors"
                        aria-label="Закрыть"
                    >
                        &times;
                    </button>
                </div>

                <div className="p-4">
                    <p className="text-gray-600 text-sm">
                        Вы точно хотите удалить этот чат? Это действие нельзя отменить.
                    </p>
                </div>

                <div className="p-4 border-t border-gray-100 flex gap-3">
                    <button
                        onClick={onCancel}
                        className="flex-1 py-2 px-4 border-2 border-gray-200 text-gray-700 font-semibold rounded-xl hover:bg-gray-50 transition-colors"
                    >
                        Отклонить
                    </button>
                    <button
                        onClick={onConfirm}
                        className="flex-1 py-2 px-4 bg-red-500 text-white font-semibold rounded-xl hover:bg-red-600 transition-colors"
                    >
                        Подтвердить
                    </button>
                </div>
            </div>
        </div>
    );
}
