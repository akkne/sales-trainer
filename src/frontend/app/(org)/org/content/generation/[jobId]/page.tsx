"use client";

import { useCallback, useMemo, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Chip } from "@/shared/components/chip";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import { PROFILE_DRAFT_HANDOFF_STORAGE_KEY } from "@/features/org-profile/constants/profile-fields";
import { CompletedRunPanel } from "@/features/org-content-generation/components/completed-run-panel";
import { FailedRunPanel } from "@/features/org-content-generation/components/failed-run-panel";
import { InsufficiencyPanel } from "@/features/org-content-generation/components/insufficiency-panel";
import { RunProgressPanel } from "@/features/org-content-generation/components/run-progress-panel";
import { SourceMaterialDisclosure } from "@/features/org-content-generation/components/source-material-disclosure";
import { StructureCheckpointCard } from "@/features/org-content-generation/components/structure-checkpoint-card";
import {
    describeJobStatus,
    describeRunOrigin,
    jobStatusTone,
} from "@/features/org-content-generation/constants/generation-dictionary";
import {
    useApproveJob,
    useContentGenerationJob,
    useRetryJob,
    useSupplementJobMaterial,
} from "@/features/org-content-generation/hooks/use-content-generation";
import { useUnarchiveProducedLesson } from "@/features/org-content-generation/hooks/use-produced-lesson";
import {
    describeContentGenerationFailure,
    isNotFoundFailure,
    readInsufficiencyFromConflict,
} from "@/features/org-content-generation/utils/api-failure";
import {
    canApproveStructure,
    canEditStructure,
    canOpenStructureFromRefusal,
    resolveJobLayout,
} from "@/features/org-content-generation/utils/job-state";
import { formatRunTimestamp } from "@/features/org-content-generation/utils/queue-copy";
import type {
    ContentGenerationJob,
    ContentInsufficiency,
} from "@/features/org-content-generation/types/content-generation";

/**
 * The «Заполнить профиль компании из этой структуры» handoff into O8.
 *
 * The reviewed structure is written to O8's own `sessionStorage` slot and the browser navigates —
 * this screen never assembles `PUT /organizations/profile` itself. That is not a shortcut but the
 * rule: a profile save is a whole-document write, and pushing a deck's reading over it would delete
 * the `bannedClaims` a compliance officer entered. O8 previews the four merge decisions and applies
 * only what a person ticks.
 */
function handOverStructureToProfile(job: ContentGenerationJob): void {
    if (typeof window === "undefined" || !job.structure) return;

    window.sessionStorage.setItem(
        PROFILE_DRAFT_HANDOFF_STORAGE_KEY,
        JSON.stringify({
            product: job.structure.product,
            icp: job.structure.icp,
            tone: job.structure.tone,
            objections: job.structure.objections,
            scriptStages: job.structure.scriptStages,
            glossary: job.structure.glossary,
            bannedClaims: job.structure.bannedClaims,
        })
    );
}

/**
 * O11 `/org/content/generation/[jobId]` (docs/TENANCY/ADMIN_UI_DESIGN.md §2 O11,
 * docs/CONTENT_PIPELINE.md).
 *
 * One screen, six statuses, five layouts — and no second progress UI for the dashboard's «сделать
 * контент по этому провалу» button, which lands here like everything else.
 *
 * **There is no «сгенерировать всё равно» anywhere on this screen, and there is no route behind
 * one.** The checkpoint and the sufficiency threshold are the two blocks that stop money being
 * spent on unusable material; a bypass cancels both, and `CK_ContentGenerationJobs_Checkpoint`
 * would refuse the resulting state in the database anyway. The refusal is arguable — add material,
 * or type the structure — and never waivable.
 */
export default function ContentGenerationRunPage() {
    const routeParameters = useParams<{ jobId: string }>();
    const jobId = routeParameters.jobId ?? "";
    const router = useRouter();

    const { data: job, isLoading, isError, error, refetch } = useContentGenerationJob(jobId);

    const supplementMaterial = useSupplementJobMaterial(jobId);
    const approveJob = useApproveJob(jobId);
    const retryJob = useRetryJob(jobId);
    const unarchiveLesson = useUnarchiveProducedLesson();

    const [isRefusedStructureOpen, setIsRefusedStructureOpen] = useState(false);
    const [wasShownToTeam, setWasShownToTeam] = useState(false);

    /**
     * The 409 the approval answers with when the structure is still too thin. The server has
     * already moved the run to `insufficient` before replying, so this is only what lets the screen
     * show the list without waiting for the re-read it also issues.
     */
    const approveConflictInsufficiency: ContentInsufficiency | null = useMemo(
        () => readInsufficiencyFromConflict(approveJob.error),
        [approveJob.error]
    );

    const handleFillProfile = useCallback(() => {
        if (!job) return;

        handOverStructureToProfile(job);
        router.push("/org/profile");
    }, [job, router]);

    if (isLoading) {
        return (
            <>
                <PageHeader title="Прогон генерации" backHref="/org/content/generation" backLabel="Прогоны" />
                <div className="flex flex-col gap-4">
                    <Skeleton height={72} rounded={16} />
                    <Skeleton height={260} rounded={16} />
                </div>
            </>
        );
    }

    if (isError && isNotFoundFailure(error)) {
        return (
            <>
                <PageHeader title="Прогон генерации" backHref="/org/content/generation" backLabel="Прогоны" />
                <EmptyState
                    icon="layers"
                    title="Прогон не найден"
                    description="Возможно, ссылка устарела или прогон удалили."
                    action={
                        <Button variant="outline" size="md" onClick={() => router.push("/org/content/generation")}>
                            Ко всем прогонам
                        </Button>
                    }
                />
            </>
        );
    }

    if (isError || !job) {
        return (
            <>
                <PageHeader title="Прогон генерации" backHref="/org/content/generation" backLabel="Прогоны" />
                <ErrorState
                    title="Не удалось загрузить прогон"
                    message="Проверьте подключение и попробуйте снова."
                    onRetry={() => refetch()}
                />
            </>
        );
    }

    const layout = resolveJobLayout(job.status);
    const insufficiency = approveConflictInsufficiency ?? job.insufficiency;
    // The editor belongs to the two states `PUT …/structure` accepts, and to no other: on a refused
    // run it appears only once somebody asked for it with «Открыть структуру».
    const isCheckpointEditorVisible =
        canEditStructure(job.status) && (layout === "checkpoint" || isRefusedStructureOpen);

    return (
        <>
            <PageHeader
                title={job.title}
                subtitle={`${describeRunOrigin(job.gapSourceRef)} · создан ${formatRunTimestamp(job.createdAt)}`}
                backHref="/org/content/generation"
                backLabel="Прогоны"
                action={
                    <Chip tone={jobStatusTone(job.status)} size="md">
                        {describeJobStatus(job.status)}
                    </Chip>
                }
            />

            <div className="flex flex-col gap-4">
                {layout === "in_progress" && (
                    <RunProgressPanel
                        status={job.status}
                        startedAtLabel={formatRunTimestamp(job.createdAt)}
                    />
                )}

                {layout === "unknown" && (
                    <Card padding={20}>
                        <p className="text-sm text-ink-2">
                            Прогон находится в состоянии «{job.status}», которого эта версия панели
                            не знает. Обновите страницу; если состояние не меняется — напишите вашему
                            менеджеру Sellevate.
                        </p>
                    </Card>
                )}

                {layout === "insufficient" && insufficiency && (
                    <InsufficiencyPanel
                        insufficiency={insufficiency}
                        canOpenStructure={canOpenStructureFromRefusal(job) && !isRefusedStructureOpen}
                        onOpenStructure={() => setIsRefusedStructureOpen(true)}
                        onSupplementMaterial={(material) =>
                            supplementMaterial.mutate(material, {
                                onSuccess: () => setIsRefusedStructureOpen(false),
                            })
                        }
                        isSupplementPending={supplementMaterial.isPending}
                        supplementErrorMessage={
                            supplementMaterial.isError
                                ? describeContentGenerationFailure(
                                      supplementMaterial.error,
                                      "supplementMaterial"
                                  )
                                : null
                        }
                    />
                )}

                {isCheckpointEditorVisible && (
                    /*
                     * Keyed on `structuredAt`: a structuring pass that lands while somebody is
                     * looking replaces the document, and remounting is what re-seeds the draft
                     * from it instead of letting the autosave write a stale one back.
                     */
                    <StructureCheckpointCard
                        key={job.structuredAt ?? "no-structure"}
                        jobId={job.id}
                        structure={job.structure}
                        canApprove={canApproveStructure(job.status)}
                        onApprove={() => approveJob.mutate()}
                        isApprovePending={approveJob.isPending}
                        approveErrorMessage={
                            approveJob.isError && !approveConflictInsufficiency
                                ? describeContentGenerationFailure(approveJob.error, "approve")
                                : null
                        }
                        onFillProfile={handleFillProfile}
                    />
                )}

                {layout === "completed" && (
                    <CompletedRunPanel
                        jobId={job.id}
                        title={job.title}
                        producedLessonId={job.producedLessonId}
                        producedExerciseCount={job.producedExerciseCount}
                        onShowToTeam={() => {
                            if (!job.producedLessonId) return;
                            unarchiveLesson.mutate(job.producedLessonId, {
                                onSuccess: () => setWasShownToTeam(true),
                            });
                        }}
                        isShowToTeamPending={unarchiveLesson.isPending}
                        wasShownToTeam={wasShownToTeam}
                        showToTeamErrorMessage={
                            unarchiveLesson.isError
                                ? describeContentGenerationFailure(
                                      unarchiveLesson.error,
                                      "unarchiveLesson"
                                  )
                                : null
                        }
                    />
                )}

                {layout === "failed" && (
                    <FailedRunPanel
                        failureReason={job.failureReason}
                        hasStructure={job.structure !== null}
                        onRetry={() => retryJob.mutate()}
                        isRetryPending={retryJob.isPending}
                        retryErrorMessage={
                            retryJob.isError
                                ? describeContentGenerationFailure(retryJob.error, "retry")
                                : null
                        }
                    />
                )}

                <SourceMaterialDisclosure
                    sourceMaterial={job.sourceMaterial}
                    gapSourceRef={job.gapSourceRef}
                />
            </div>
        </>
    );
}
