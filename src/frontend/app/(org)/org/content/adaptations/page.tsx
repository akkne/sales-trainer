"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Tabs } from "@/shared/components/tabs";
import { getStageMeta } from "@/features/skills/constants/skill-stages";
import { useSkillStages } from "@/features/skills/hooks/use-skill-tree";
import { StartAdaptationDialog } from "@/features/org-content-adaptation/components/start-adaptation-dialog";
import {
    ADAPTATION_MODE_LABELS,
    describeAdaptationModeShort,
    describeJobStatus,
    jobStatusTone,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import { useContentAdaptationJobs } from "@/features/org-content-adaptation/hooks/use-content-adaptations";
import type {
    ContentAdaptationJobSummary,
    ContentAdaptationMode,
} from "@/features/org-content-adaptation/types/adaptation";
import { formatExerciseCount } from "@/features/org-content-adaptation/utils/proposal-queue";

const EMPTY_STATE_COPY: Record<ContentAdaptationMode, { title: string; description: string }> = {
    tone_rewrite: {
        title: "Пакетов правки ещё нет",
        description:
            "Выберите этап воронки — модель предложит, как переписать его упражнения под ваш продукт и тон. Ни одна правка не попадёт в контент, пока вы её не примете.",
    },
    quality_review: {
        title: "Ревью ещё не запускали",
        description:
            "Модель прочитает упражнения этапа и скажет, что с ними не так по существу: неоднозначный ответ, очевидные дистракторы, критерии, которые нельзя проверить. Исправлять всё равно вам — ревью только показывает.",
    },
};

/**
 * O12 «Массовая правка и ревью» (docs/TENANCY/ADMIN_UI_DESIGN.md §2).
 *
 * <b>The number in the heading column is `awaitingReviewCount`, not `pendingCount`.</b> A batch is
 * not done when the model finishes — it is done when a person has answered every proposal, and
 * showing the model's progress as the headline number would call a batch finished at the exact
 * moment its actual work begins.
 *
 * The whole list arrives in one read and the two tabs split it on the client: the route takes a
 * `mode` filter but does not paginate, and two requests would only buy two chances to disagree
 * about how many batches exist.
 */
export default function ContentAdaptationsPage() {
    const router = useRouter();
    const { stages } = useSkillStages();
    const jobsQuery = useContentAdaptationJobs();
    const [activeMode, setActiveMode] = useState<ContentAdaptationMode>("tone_rewrite");
    const [isStartDialogOpen, setIsStartDialogOpen] = useState(false);

    const jobs = useMemo(() => jobsQuery.data ?? [], [jobsQuery.data]);

    const awaitingCountByMode = useMemo(() => {
        const counts: Record<string, number> = {};
        for (const job of jobs) {
            if (job.awaitingReviewCount === 0) continue;
            counts[job.mode] = (counts[job.mode] ?? 0) + 1;
        }
        return counts;
    }, [jobs]);

    const visibleJobs = useMemo(
        () => jobs.filter((job) => job.mode === activeMode),
        [jobs, activeMode]
    );

    const columns: Column<ContentAdaptationJobSummary>[] = [
        {
            key: "stage",
            header: "Этап",
            render: (job) => (
                <span className="font-medium text-ink">
                    {getStageMeta(job.stageKey, stages).label}
                </span>
            ),
        },
        {
            key: "mode",
            header: "Режим",
            render: (job) => (
                <span className="text-ink-3">{describeAdaptationModeShort(job.mode)}</span>
            ),
        },
        {
            key: "awaiting",
            header: "Ждут ответа",
            align: "right",
            render: (job) => (
                <span className="tnum" style={{ fontFamily: "var(--font-mono)" }}>
                    {job.awaitingReviewCount}
                    <span className="text-ink-3"> / {job.itemCount}</span>
                </span>
            ),
        },
        {
            key: "answered",
            header: "Принято / отклонено",
            align: "right",
            render: (job) => (
                <span className="tnum text-ink-3" style={{ fontFamily: "var(--font-mono)" }}>
                    {job.acceptedCount} / {job.rejectedCount}
                </span>
            ),
        },
        {
            key: "status",
            header: "Статус",
            render: (job) => (
                <Chip tone={jobStatusTone(job.status)} size="sm">
                    {describeJobStatus(job.status)}
                </Chip>
            ),
        },
    ];

    if (jobsQuery.isError) {
        return (
            <>
                <PageHeader title="Массовая правка и ревью" />
                <ErrorState
                    title="Не удалось загрузить пакеты"
                    message="Проверьте подключение и попробуйте снова."
                    onRetry={() => jobsQuery.refetch()}
                />
            </>
        );
    }

    return (
        <>
            <PageHeader
                title="Массовая правка и ревью"
                subtitle="Пакет — это один этап воронки. Модель предлагает, отвечаете вы: по одному упражнению за раз."
                action={
                    <Button variant="primary" onClick={() => setIsStartDialogOpen(true)}>
                        Переписать этап под свой продукт
                    </Button>
                }
            />

            <Tabs
                className="mb-5"
                activeKey={activeMode}
                onChange={(key) => setActiveMode(key as ContentAdaptationMode)}
                items={(Object.keys(ADAPTATION_MODE_LABELS) as ContentAdaptationMode[]).map(
                    (mode) => ({
                        key: mode,
                        label: ADAPTATION_MODE_LABELS[mode],
                        badge: awaitingCountByMode[mode],
                    })
                )}
            />

            <DataTable
                columns={columns}
                rows={visibleJobs}
                rowKey={(job) => job.id}
                isLoading={jobsQuery.isLoading}
                onRowClick={(job) => router.push(`/org/content/adaptations/${job.id}`)}
                empty={
                    <EmptyState
                        icon="layers"
                        title={EMPTY_STATE_COPY[activeMode].title}
                        description={EMPTY_STATE_COPY[activeMode].description}
                        action={
                            <Button variant="secondary" onClick={() => setIsStartDialogOpen(true)}>
                                Выбрать этап
                            </Button>
                        }
                    />
                }
            />

            {visibleJobs.length > 0 && (
                <p className="mt-4 text-xs text-ink-3">
                    Всего в очередях этой вкладки:{" "}
                    {formatExerciseCount(
                        visibleJobs.reduce((total, job) => total + job.itemCount, 0)
                    )}
                    . Кнопки «применить всё» нет: если очередь кажется длинной — этап был слишком широк.
                </p>
            )}

            {isStartDialogOpen && (
                <StartAdaptationDialog
                    open
                    mode={activeMode}
                    onClose={() => setIsStartDialogOpen(false)}
                    onStarted={(jobId) => {
                        setIsStartDialogOpen(false);
                        router.push(`/org/content/adaptations/${jobId}`);
                    }}
                />
            )}
        </>
    );
}
