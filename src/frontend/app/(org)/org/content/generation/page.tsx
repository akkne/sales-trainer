"use client";

import { Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { StartGenerationModal } from "@/features/org-content-generation/components/start-generation-modal";
import {
    CONTENT_GENERATION_STATUS_FILTER_ORDER,
    describeJobStatus,
    describeRunOrigin,
    jobStatusTone,
} from "@/features/org-content-generation/constants/generation-dictionary";
import {
    useContentGenerationJobs,
    useStartContentGeneration,
} from "@/features/org-content-generation/hooks/use-content-generation";
import { describeContentGenerationFailure } from "@/features/org-content-generation/utils/api-failure";
import { firstSufficiencyGapMessage } from "@/features/org-content-generation/utils/job-state";
import { formatRunTimestamp } from "@/features/org-content-generation/utils/queue-copy";
import type { ContentGenerationJobSummary } from "@/features/org-content-generation/types/content-generation";

/** `?new=1` is how O9's «Сделать урок из материалов» opens the form on arrival. */
const OPEN_START_FORM_PARAMETER = "new";

function GenerationRunsContent() {
    const router = useRouter();
    const searchParameters = useSearchParams();

    const [statusFilter, setStatusFilter] = useState<string | null>(null);
    /**
     * Read once, at mount, rather than watched: the parameter is a hand-off from O9's button, not a
     * piece of state the address bar owns. Watching it would reopen the form every time the router
     * re-renders after the РОП closed it.
     */
    const [isStartModalOpen, setIsStartModalOpen] = useState(
        () => searchParameters.get(OPEN_START_FORM_PARAMETER) === "1"
    );

    const { data: runs, isLoading, isError, refetch } = useContentGenerationJobs(statusFilter);
    const startGeneration = useStartContentGeneration();

    /**
     * O-9 (docs/AUDIT_PROD.md): closing the modal has to drop `?new=1` too, or a reload — or even
     * just the back button — reopens a form the РОП just closed. Only replaces when the parameter
     * is actually there, so closing a modal opened by the button (no query string to begin with)
     * doesn't add a needless history entry.
     */
    const closeStartModal = () => {
        setIsStartModalOpen(false);
        if (searchParameters.has(OPEN_START_FORM_PARAMETER)) {
            router.replace("/org/content/generation");
        }
    };

    const columns: Column<ContentGenerationJobSummary>[] = [
        {
            key: "title",
            header: "Название",
            render: (run) => {
                const refusalHeadline = firstSufficiencyGapMessage(run.insufficiency);

                return (
                    <div className="min-w-0">
                        <p className="font-medium text-ink">{run.title}</p>
                        {refusalHeadline && (
                            <p className="mt-0.5 text-xs text-ink-3">{refusalHeadline}</p>
                        )}
                        {!refusalHeadline && run.failureReason && (
                            <p className="mt-0.5 text-xs text-ink-3">{run.failureReason}</p>
                        )}
                    </div>
                );
            },
        },
        {
            key: "status",
            header: "Статус",
            render: (run) => (
                <Chip tone={jobStatusTone(run.status)} size="sm">
                    {describeJobStatus(run.status)}
                </Chip>
            ),
        },
        {
            key: "origin",
            header: "Откуда",
            render: (run) => (
                <span className="text-xs text-ink-3">{describeRunOrigin(run.gapSourceRef)}</span>
            ),
        },
        {
            key: "exercises",
            header: "Упражнений",
            align: "right",
            render: (run) => (
                <span className="mono text-ink-2">
                    {run.producedLessonId ? run.producedExerciseCount : "—"}
                </span>
            ),
        },
        {
            key: "createdAt",
            header: "Создан",
            align: "right",
            render: (run) => (
                <span className="mono text-xs text-ink-3">{formatRunTimestamp(run.createdAt)}</span>
            ),
        },
    ];

    const handleStart = (request: { title: string; material: string }) => {
        startGeneration.mutate(request, {
            onSuccess: (job) => {
                setIsStartModalOpen(false);
                router.push(`/org/content/generation/${job.id}`);
            },
        });
    };

    return (
        <>
            <PageHeader
                title="Прогоны генерации"
                subtitle="Материал → структура, которую вы подтверждаете → упражнения. Ничего не генерируется, пока вы не сказали «всё верно»."
                backHref="/org/content"
                backLabel="Контент"
                action={
                    <Button
                        variant="primary"
                        size="md"
                        onClick={() => setIsStartModalOpen(true)}
                        iconLeft="plus"
                    >
                        Сделать урок из материалов
                    </Button>
                }
            />

            <div className="mb-4 flex flex-wrap items-center gap-2">
                <Chip
                    tone="neutral"
                    size="sm"
                    active={statusFilter === null}
                    onClick={() => setStatusFilter(null)}
                >
                    Все
                </Chip>
                {CONTENT_GENERATION_STATUS_FILTER_ORDER.map((status) => (
                    <Chip
                        key={status}
                        tone="neutral"
                        size="sm"
                        active={statusFilter === status}
                        onClick={() => setStatusFilter(status)}
                    >
                        {describeJobStatus(status)}
                    </Chip>
                ))}
            </div>

            {isError ? (
                <ErrorState
                    title="Не удалось загрузить прогоны"
                    message="Проверьте подключение и попробуйте снова."
                    onRetry={() => refetch()}
                />
            ) : (
                <DataTable
                    columns={columns}
                    rows={runs ?? []}
                    rowKey={(run) => run.id}
                    isLoading={isLoading}
                    onRowClick={(run) => router.push(`/org/content/generation/${run.id}`)}
                    empty={
                        <EmptyState
                            icon="layers"
                            title={
                                statusFilter === null
                                    ? "Прогонов ещё не было"
                                    : "В этом статусе прогонов нет"
                            }
                            description={
                                statusFilter === null
                                    ? "Загрузите материалы внутреннего тренинга — ИИ разберёт их, покажет структуру, вы её поправите, и только потом появятся упражнения."
                                    : "Снимите фильтр, чтобы увидеть остальные прогоны."
                            }
                            action={
                                statusFilter === null ? (
                                    <Button
                                        variant="primary"
                                        size="md"
                                        onClick={() => setIsStartModalOpen(true)}
                                    >
                                        Сделать урок из материалов
                                    </Button>
                                ) : undefined
                            }
                        />
                    }
                />
            )}

            {/* Mounted only while open, so a cancelled draft is gone rather than reset by hand. */}
            {isStartModalOpen && (
                <StartGenerationModal
                    onClose={closeStartModal}
                    onStart={handleStart}
                    isPending={startGeneration.isPending}
                    startErrorMessage={
                        startGeneration.isError
                            ? describeContentGenerationFailure(startGeneration.error, "start")
                            : null
                    }
                />
            )}
        </>
    );
}

/**
 * O10 «Прогоны генерации» (docs/TENANCY/ADMIN_UI_DESIGN.md §2 O10).
 *
 * A refused run prints its first gap **in the row**: `ContentGenerationJobSummaryDto` carries the
 * refusal specifically so this list can, and without it every refused run costs a trip inside to
 * find out what it wants.
 */
export default function GenerationRunsPage() {
    return (
        <Suspense fallback={null}>
            <GenerationRunsContent />
        </Suspense>
    );
}
