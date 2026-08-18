"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { Select } from "@/shared/components/input";
import { useSkillStages } from "@/features/skills/hooks/use-skill-tree";
import {
    ADAPTATION_MODE_LABELS,
    describeAdaptationMode,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import {
    useContentAdaptationJobs,
    useStartContentAdaptation,
} from "@/features/org-content-adaptation/hooks/use-content-adaptations";
import type { ContentAdaptationMode } from "@/features/org-content-adaptation/types/adaptation";
import { describeStartFailure } from "@/features/org-content-adaptation/utils/adaptation-failure";

const LIVE_JOB_STATUSES = ["preparing", "awaiting_review"];

interface StartAdaptationDialogProps {
    open: boolean;
    mode: ContentAdaptationMode;
    /** Preselected stage — how «Переписать этот этап под нас» carries O13's `stageKey` across. */
    initialStageKey?: string;
    onClose: () => void;
    onStarted: (jobId: string) => void;
}

/**
 * «Переписать этап под свой продукт» (O12). Two fields, because the request has two: a stage and a
 * purpose.
 *
 * <b>There is no exercise picker and there cannot be one.</b> The server resolves the scope through
 * 40.18's read resolution — the organization's own copy of a lesson where it has forked one, the
 * global row where it has not — and a caller who could name exercise ids could ask it to rewrite the
 * wrong half of a fork.
 */
export function StartAdaptationDialog({
    open,
    mode,
    initialStageKey = "",
    onClose,
    onStarted,
}: StartAdaptationDialogProps) {
    const { stages, isLoading: areStagesLoading } = useSkillStages();
    /// Read only so that a 409 can name the batch it is refusing in favour of. The list is the same
    /// cached query O12 already holds, so opening this dialog from the queue screen costs one read.
    const jobsQuery = useContentAdaptationJobs();
    const [selectedStageKey, setSelectedStageKey] = useState(initialStageKey);
    const [selectedMode, setSelectedMode] = useState<ContentAdaptationMode>(mode);
    const [failureMessage, setFailureMessage] = useState<string | null>(null);
    const [conflictingJobId, setConflictingJobId] = useState<string | null>(null);

    const startAdaptationMutation = useStartContentAdaptation();

    const stageOptions = useMemo(
        () => [...stages].sort((left, right) => left.order - right.order),
        [stages]
    );

    const resolvedStageKey = selectedStageKey || stageOptions[0]?.key || "";

    const handleSubmit = () => {
        if (resolvedStageKey.length === 0) return;

        setFailureMessage(null);
        setConflictingJobId(null);

        startAdaptationMutation.mutate(
            { mode: selectedMode, stageKey: resolvedStageKey },
            {
                onSuccess: (job) => onStarted(job.summary.id),
                onError: (error) => {
                    const failure = describeStartFailure(error);
                    setFailureMessage(failure.message);

                    if (!failure.isLiveBatchConflict) return;

                    const liveJob = (jobsQuery.data ?? []).find(
                        (job) =>
                            job.mode === selectedMode &&
                            job.stageKey === resolvedStageKey &&
                            LIVE_JOB_STATUSES.includes(job.status)
                    );
                    setConflictingJobId(liveJob?.id ?? null);
                },
            }
        );
    };

    return (
        <Modal
            open={open}
            onClose={onClose}
            title="Пакет по этапу"
            size="md"
            footer={
                <>
                    <Button variant="ghost" onClick={onClose} disabled={startAdaptationMutation.isPending}>
                        Отмена
                    </Button>
                    <Button
                        variant="primary"
                        onClick={handleSubmit}
                        loading={startAdaptationMutation.isPending}
                        disabled={resolvedStageKey.length === 0}
                    >
                        Запустить
                    </Button>
                </>
            }
        >
            <div className="flex flex-col gap-4">
                <Select
                    label="Этап"
                    hint="В пакет попадут все упражнения этапа: ваши версии там, где вы их сделали, и библиотечные там, где нет."
                    value={resolvedStageKey}
                    disabled={areStagesLoading}
                    onChange={(changeEvent) => setSelectedStageKey(changeEvent.target.value)}
                >
                    {stageOptions.map((stage) => (
                        <option key={stage.key} value={stage.key}>
                            {stage.label}
                        </option>
                    ))}
                </Select>

                <Select
                    label="Что сделать"
                    value={selectedMode}
                    onChange={(changeEvent) =>
                        setSelectedMode(changeEvent.target.value as ContentAdaptationMode)
                    }
                >
                    {(Object.keys(ADAPTATION_MODE_LABELS) as ContentAdaptationMode[]).map(
                        (modeOption) => (
                            <option key={modeOption} value={modeOption}>
                                {describeAdaptationMode(modeOption)}
                            </option>
                        )
                    )}
                </Select>

                <p className="text-xs text-ink-3">
                    Запуск ничего не тратит: модель отвечает по одному упражнению за раз, и каждое
                    предложение ждёт вашего ответа. Ничего не применяется само.
                </p>

                {failureMessage && (
                    <div
                        role="alert"
                        className="rounded-xl p-3 text-sm"
                        style={{ background: "var(--warn-soft)", color: "var(--ink-2)" }}
                    >
                        {failureMessage}
                        {conflictingJobId && (
                            <Link
                                href={`/org/content/adaptations/${conflictingJobId}`}
                                className="block mt-2 underline"
                                onClick={onClose}
                            >
                                Открыть этот пакет
                            </Link>
                        )}
                    </div>
                )}
            </div>
        </Modal>
    );
}
