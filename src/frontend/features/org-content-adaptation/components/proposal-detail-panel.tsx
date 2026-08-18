"use client";

import Link from "next/link";
import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { Skeleton } from "@/shared/components/skeleton";
import {
    ACCEPT_PUBLISHING_CAVEAT,
    REVIEW_MODE_REJECT_CAVEAT,
    describeExerciseType,
    describeItemStatus,
    itemStatusTone,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import {
    useAcceptAdaptationItem,
    useContentAdaptationItem,
    useRejectAdaptationItem,
} from "@/features/org-content-adaptation/hooks/use-content-adaptations";
import { FindingList } from "@/features/org-content-adaptation/components/finding-list";
import { ProposalDiffView } from "@/features/org-content-adaptation/components/proposal-diff-view";
import { describeItemActionFailure } from "@/features/org-content-adaptation/utils/adaptation-failure";
import { describeItemActions } from "@/features/org-content-adaptation/utils/proposal-queue";

/**
 * The right column of O13 — one proposal, and the two verbs that answer it.
 *
 * <b>«Принять» exists only in rewrite mode.</b> A review finding is a diagnosis: the accept route
 * answers 409 for one, and so does the database. Instead the review side offers the two things that
 * actually fix an exercise — opening it in the editor, or asking for a rewrite of the whole stage.
 *
 * <b>And the caption under «Принять» is not decoration.</b> Accepting writes the exercise draft;
 * the team meets it only when somebody publishes a lesson version. Without the sentence a РОП
 * answers forty proposals and then waits for a trainer that still plays the old text.
 */

interface ProposalDetailPanelProps {
    jobId: string;
    mode: string;
    itemId: string | null;
    /** `null` when this is the last unanswered item — the button disappears rather than looping. */
    nextAwaitingItemId: string | null;
    onOpenNext: (itemId: string) => void;
    /** Review mode: start a `tone_rewrite` batch over the same stage. */
    onRewriteStage: () => void;
}

export function ProposalDetailPanel({
    jobId,
    mode,
    itemId,
    nextAwaitingItemId,
    onOpenNext,
    onRewriteStage,
}: ProposalDetailPanelProps) {
    const itemQuery = useContentAdaptationItem(jobId, itemId);
    const acceptItemMutation = useAcceptAdaptationItem(jobId);
    const rejectItemMutation = useRejectAdaptationItem(jobId);
    const [failureMessage, setFailureMessage] = useState<string | null>(null);

    if (itemId === null) {
        return (
            <EmptyState
                icon="layers"
                title="Выберите предложение слева"
                description="Очередь разбирается по одному упражнению: так каждая правка попадает в контент только после того, как её кто-то прочитал."
            />
        );
    }

    if (itemQuery.isLoading) {
        return (
            <div className="flex flex-col gap-3">
                <Skeleton height={24} rounded={8} />
                <Skeleton height={72} rounded={12} />
                <Skeleton height={160} rounded={12} />
            </div>
        );
    }

    if (itemQuery.isError || !itemQuery.data) {
        return (
            <ErrorState
                title="Не удалось загрузить предложение"
                message="Проверьте подключение и попробуйте снова."
                onRetry={() => itemQuery.refetch()}
            />
        );
    }

    const item = itemQuery.data;
    const isReviewMode = mode === "quality_review";
    const actions = describeItemActions(item.summary.status, mode, item.isStale);
    const isAnswering = acceptItemMutation.isPending || rejectItemMutation.isPending;

    const answerItem = (answer: "accept" | "reject") => {
        setFailureMessage(null);
        const mutation = answer === "accept" ? acceptItemMutation : rejectItemMutation;

        mutation.mutate(item.summary.id, {
            onSuccess: () => {
                if (nextAwaitingItemId !== null) onOpenNext(nextAwaitingItemId);
            },
            onError: (error) => setFailureMessage(describeItemActionFailure(error)),
        });
    };

    return (
        <div className="flex flex-col gap-5">
            <header className="flex flex-wrap items-center gap-2">
                <h2 className="text-base font-bold text-ink">{item.summary.lessonTitle}</h2>
                <span className="text-sm text-ink-3">
                    · {describeExerciseType(item.summary.exerciseType)}
                </span>
                <Chip tone={itemStatusTone(item.summary.status)} size="sm">
                    {describeItemStatus(item.summary.status)}
                </Chip>
            </header>

            {item.summary.failureReason && (
                <p
                    className="rounded-xl p-3 text-sm"
                    style={{ background: "var(--bad-soft)", color: "var(--bad)" }}
                >
                    {item.summary.failureReason}
                </p>
            )}

            {isReviewMode ? (
                <FindingList findings={item.findings} />
            ) : (
                <ProposalDiffView
                    changeSummary={item.summary.changeSummary}
                    changes={item.changes}
                    hasProposedContent={item.proposedContent !== null && item.proposedContent !== undefined}
                />
            )}

            <div style={{ borderTop: "1px solid var(--line)" }} />

            {actions.acceptBlockedReason && !isReviewMode && (
                <p className="text-sm text-ink-3">{actions.acceptBlockedReason}</p>
            )}

            {failureMessage && (
                <p
                    role="alert"
                    className="rounded-xl p-3 text-sm"
                    style={{ background: "var(--warn-soft)", color: "var(--ink-2)" }}
                >
                    {failureMessage}
                </p>
            )}

            <div className="flex flex-wrap items-center gap-2">
                {!isReviewMode && (
                    <Button
                        variant="primary"
                        onClick={() => answerItem("accept")}
                        disabled={!actions.canAccept || isAnswering}
                        loading={acceptItemMutation.isPending}
                    >
                        Принять
                    </Button>
                )}

                <Button
                    variant="secondary"
                    onClick={() => answerItem("reject")}
                    disabled={!actions.canReject || isAnswering}
                    loading={rejectItemMutation.isPending}
                >
                    Отклонить
                </Button>

                {isReviewMode && (
                    <>
                        <Link href={`/org/content/lessons/${item.summary.lessonId}`}>
                            <Button variant="ghost">Открыть упражнение</Button>
                        </Link>
                        <Button variant="ghost" onClick={onRewriteStage}>
                            Переписать этот этап под нас
                        </Button>
                    </>
                )}

                {nextAwaitingItemId !== null && (
                    <Button
                        variant="ghost"
                        className="ml-auto"
                        onClick={() => onOpenNext(nextAwaitingItemId)}
                    >
                        Следующее →
                    </Button>
                )}
            </div>

            <p className="text-xs text-ink-3">
                {isReviewMode ? REVIEW_MODE_REJECT_CAVEAT : ACCEPT_PUBLISHING_CAVEAT}
            </p>
        </div>
    );
}
