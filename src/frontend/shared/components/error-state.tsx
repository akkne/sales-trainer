"use client";

import { Icon } from "./icon";
import { Button } from "./button";

interface ErrorStateProps {
    title?: string;
    message?: string;
    /** Shows a retry button when provided. */
    onRetry?: () => void;
    retryLabel?: string;
    compact?: boolean;
}

/** Shared data-fetch error block with optional retry. */
export function ErrorState({
    title = "Что-то пошло не так",
    message = "Не удалось загрузить данные. Проверьте соединение и попробуйте ещё раз.",
    onRetry,
    retryLabel = "Повторить",
    compact = false,
}: ErrorStateProps) {
    return (
        <div
            className={`flex flex-col items-center justify-center text-center ${compact ? "py-8" : "py-16"}`}
            role="alert"
        >
            <div className="w-14 h-14 rounded-2xl bg-bad-soft flex items-center justify-center mb-4">
                <Icon name="warning" size="lg" className="text-bad" />
            </div>
            <h3 className="font-medium text-ink mb-1">{title}</h3>
            <p className="text-sm text-ink-3 max-w-sm mb-5">{message}</p>
            {onRetry && (
                <Button variant="secondary" onClick={onRetry}>
                    {retryLabel}
                </Button>
            )}
        </div>
    );
}
