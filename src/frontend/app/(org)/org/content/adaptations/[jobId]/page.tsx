"use client";

import { useMemo, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Card, CardContent } from "@/shared/components/card";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import { getStageMeta } from "@/features/skills/constants/skill-stages";
import { useSkillStages } from "@/features/skills/hooks/use-skill-tree";
import { BatchStatusPanel } from "@/features/org-content-adaptation/components/batch-status-panel";
import { ProposalDetailPanel } from "@/features/org-content-adaptation/components/proposal-detail-panel";
import { ProposalQueueList } from "@/features/org-content-adaptation/components/proposal-queue-list";
import { StartAdaptationDialog } from "@/features/org-content-adaptation/components/start-adaptation-dialog";
import { describeAdaptationMode } from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import {
    useContentAdaptationJob,
    useRetryContentAdaptation,
} from "@/features/org-content-adaptation/hooks/use-content-adaptations";
import {
    describeItemActionFailure,
    isNotFoundFailure,
} from "@/features/org-content-adaptation/utils/adaptation-failure";
import {
    findInitialItemId,
    findNextAwaitingItemId,
} from "@/features/org-content-adaptation/utils/proposal-queue";

const ADAPTATIONS_LIST_PATH = "/org/content/adaptations";

/**
 * O13 «Очередь предложений» (docs/TENANCY/ADMIN_UI_DESIGN.md §2).
 *
 * <b>Two columns and no third button.</b> The list on the left, one proposal on the right, accept or
 * reject aimed at exactly one item id. «Применить всё» is absent here because it is absent from the
 * backend and absent on purpose: the value of a batch is that a person read each rewrite before it
 * became their team's content, and a bulk verb is auto-apply with somebody's name attached
 * (docs/CONTENT_PIPELINE.md §6a).
 *
 * A sweep that is still working is the screen's ordinary state — it answers four exercises a tick —
 * so the queue renders while it fills, and the progress sits above it rather than replacing it.
 */
export default function ContentAdaptationQueuePage() {
    const router = useRouter();
    const routeParameters = useParams();
    const jobId = String(routeParameters.jobId ?? "");

    const { stages } = useSkillStages();
    const jobQuery = useContentAdaptationJob(jobId);
    const retryJobMutation = useRetryContentAdaptation(jobId);

    const [openedItemId, setOpenedItemId] = useState<string | null>(null);
    const [retryFailureMessage, setRetryFailureMessage] = useState<string | null>(null);
    const [isRewriteDialogOpen, setIsRewriteDialogOpen] = useState(false);

    const job = jobQuery.data;
    const items = useMemo(() => job?.items ?? [], [job]);
    const mode = job?.summary.mode ?? "";

    /// The queue arrives after the first render and grows on every poll, so the opening selection is
    /// derived rather than stored: until somebody has opened something themselves, the screen shows
    /// the first item still waiting. Writing it into state from an effect would make a background
    /// refetch capable of moving the proposal out from under the person reading it.
    const selectedItemId = useMemo(
        () => openedItemId ?? findInitialItemId(items, mode),
        [openedItemId, items, mode]
    );

    if (jobQuery.isLoading) {
        return (
            <>
                <PageHeader
                    title="Очередь предложений"
                    backHref={ADAPTATIONS_LIST_PATH}
                    backLabel="Массовая правка"
                />
                <Skeleton height={72} rounded={16} />
                <div className="mt-4 flex flex-col gap-2">
                    {[0, 1, 2, 3, 4].map((rowIndex) => (
                        <Skeleton key={rowIndex} height={40} rounded={12} />
                    ))}
                </div>
            </>
        );
    }

    if (jobQuery.isError || !job) {
        return (
            <>
                <PageHeader
                    title="Очередь предложений"
                    backHref={ADAPTATIONS_LIST_PATH}
                    backLabel="Массовая правка"
                />
                <ErrorState
                    title={
                        isNotFoundFailure(jobQuery.error)
                            ? "Пакет не найден"
                            : "Не удалось загрузить пакет"
                    }
                    message={
                        isNotFoundFailure(jobQuery.error)
                            ? "Возможно, его удалили или ссылка ведёт в чужую организацию."
                            : "Проверьте подключение и попробуйте снова."
                    }
                    onRetry={() => jobQuery.refetch()}
                />
            </>
        );
    }

    const stageLabel = getStageMeta(job.summary.stageKey, stages).label;
    const nextAwaitingItemId = findNextAwaitingItemId(items, mode, selectedItemId);

    return (
        <>
            <PageHeader
                title={`${stageLabel} · ${describeAdaptationMode(job.summary.mode)}`}
                subtitle={`Ждут ответа: ${job.summary.awaitingReviewCount} из ${job.summary.itemCount}`}
                backHref={ADAPTATIONS_LIST_PATH}
                backLabel="Массовая правка"
            />

            <BatchStatusPanel
                summary={job.summary}
                items={items}
                isRetrying={retryJobMutation.isPending}
                retryFailureMessage={retryFailureMessage}
                onRetry={() => {
                    setRetryFailureMessage(null);
                    retryJobMutation.mutate(undefined, {
                        onError: (error) =>
                            setRetryFailureMessage(describeItemActionFailure(error)),
                    });
                }}
            />

            {items.length === 0 ? (
                <EmptyState
                    icon="layers"
                    title="В этом пакете нет упражнений"
                    description="Этап оказался пустым — предлагать нечего. Выберите другой этап на предыдущем экране."
                />
            ) : (
                <div className="grid gap-4 md:grid-cols-[minmax(220px,300px)_1fr] items-start">
                    <Card>
                        <CardContent style={{ marginTop: 0 }}>
                            <ProposalQueueList
                                items={items}
                                mode={mode}
                                selectedItemId={selectedItemId}
                                onSelect={setOpenedItemId}
                            />
                        </CardContent>
                    </Card>

                    <Card>
                        <CardContent style={{ marginTop: 0 }}>
                            <ProposalDetailPanel
                                jobId={jobId}
                                mode={mode}
                                itemId={selectedItemId}
                                nextAwaitingItemId={nextAwaitingItemId}
                                onOpenNext={setOpenedItemId}
                                onRewriteStage={() => setIsRewriteDialogOpen(true)}
                            />
                        </CardContent>
                    </Card>
                </div>
            )}

            {isRewriteDialogOpen && (
                <StartAdaptationDialog
                    open
                    mode="tone_rewrite"
                    initialStageKey={job.summary.stageKey}
                    onClose={() => setIsRewriteDialogOpen(false)}
                    onStarted={(startedJobId) => {
                        setIsRewriteDialogOpen(false);
                        router.push(`${ADAPTATIONS_LIST_PATH}/${startedJobId}`);
                    }}
                />
            )}
        </>
    );
}
