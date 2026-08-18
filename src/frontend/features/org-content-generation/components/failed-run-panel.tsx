"use client";

import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Icon } from "@/shared/components/icon";

interface FailedRunPanelProps {
    failureReason: string | null;
    /** True when a structure already exists — the retry resumes generation, not structuring. */
    hasStructure: boolean;
    onRetry: () => void;
    isRetryPending: boolean;
    retryErrorMessage: string | null;
}

/**
 * O11 layout (д). A retry resumes the half that failed rather than starting over — a failed
 * generation must not re-pay for structuring — so the button says which half it is about to redo.
 */
export function FailedRunPanel({
    failureReason,
    hasStructure,
    onRetry,
    isRetryPending,
    retryErrorMessage,
}: FailedRunPanelProps) {
    return (
        <Card padding={24}>
            <div className="flex items-start gap-3">
                <Icon name="warning" size="md" style={{ color: "var(--bad)" }} />
                <div className="min-w-0 flex-1">
                    <h2 className="text-base font-bold text-ink">Прогон остановился на ошибке</h2>
                    <p className="mt-1 text-sm text-ink-2">
                        {failureReason ?? "Причина не записана."}
                    </p>
                    <p className="mt-3 text-xs text-ink-3">
                        {hasStructure
                            ? "Повтор продолжит с генерации упражнений — за уже разобранный материал платить второй раз не придётся."
                            : "Повтор начнёт разбор материала заново."}
                    </p>

                    <div className="mt-4">
                        <Button
                            variant="outline"
                            size="md"
                            loading={isRetryPending}
                            onClick={onRetry}
                        >
                            Повторить
                        </Button>
                    </div>

                    {retryErrorMessage && (
                        <p className="mt-3 text-xs" style={{ color: "var(--bad)" }} role="alert">
                            {retryErrorMessage}
                        </p>
                    )}
                </div>
            </div>
        </Card>
    );
}
