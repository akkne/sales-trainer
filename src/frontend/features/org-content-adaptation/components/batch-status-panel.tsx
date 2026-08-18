"use client";

import Link from "next/link";
import { Button } from "@/shared/components/button";
import { Progress } from "@/shared/components/progress";
import type {
    ContentAdaptationItemSummary,
    ContentAdaptationJobSummary,
} from "@/features/org-content-adaptation/types/adaptation";
import {
    canRetryJob,
    collectLessonsWithAcceptedItems,
    countItemsAnsweredByModel,
    describeResolvedBatch,
    shouldPollJob,
} from "@/features/org-content-adaptation/utils/proposal-queue";

/**
 * The three things a batch can be saying about itself besides its queue (O13 «Состояния»).
 *
 * <b>A sweep still running is the ordinary state of this screen, not an edge case.</b> It answers
 * four exercises per tick, so a person who opened a stage of forty is going to watch this bar for a
 * few minutes — which is why it shows the count rather than a spinner, and why it says the page can
 * be closed.
 */

interface BatchStatusPanelProps {
    summary: ContentAdaptationJobSummary;
    items: readonly ContentAdaptationItemSummary[];
    onRetry: () => void;
    isRetrying: boolean;
    retryFailureMessage: string | null;
}

export function BatchStatusPanel({
    summary,
    items,
    onRetry,
    isRetrying,
    retryFailureMessage,
}: BatchStatusPanelProps) {
    const isPreparing = shouldPollJob(summary.status);
    const answeredByModel = countItemsAnsweredByModel(summary);
    const isFullyAnswered = summary.awaitingReviewCount === 0 && !isPreparing;
    const lessonsToPublish = isFullyAnswered ? collectLessonsWithAcceptedItems(items) : [];

    return (
        <div className="flex flex-col gap-4 mb-6">
            {isPreparing && (
                <div className="flex flex-col gap-2">
                    <div className="flex items-baseline justify-between gap-3">
                        <span className="text-sm text-ink-2">Готовим предложения</span>
                        <span
                            className="tnum text-sm text-ink-3"
                            style={{ fontFamily: "var(--font-mono)" }}
                        >
                            {answeredByModel} / {summary.itemCount}
                        </span>
                    </div>
                    <Progress value={answeredByModel} max={summary.itemCount} tone="indigo" />
                    <p className="text-xs text-ink-3">
                        Одна модель на упражнение — это минуты, а не секунды. Страницу можно закрыть:
                        пакет продолжится и дождётся вас здесь.
                    </p>
                </div>
            )}

            {(summary.failureReason || canRetryJob(summary)) && (
                <div
                    className="rounded-xl p-4 flex flex-col gap-2"
                    style={{ background: "var(--bad-soft)" }}
                >
                    <p className="text-sm" style={{ color: "var(--bad)" }}>
                        {summary.failureReason ??
                            `Не удалось получить предложения по ${summary.failedCount} упражнениям.`}
                    </p>
                    {canRetryJob(summary) && (
                        <Button
                            variant="secondary"
                            size="sm"
                            onClick={onRetry}
                            loading={isRetrying}
                            className="self-start"
                        >
                            Повторить неудавшиеся
                        </Button>
                    )}
                    {retryFailureMessage && (
                        <p role="alert" className="text-sm text-ink-2">
                            {retryFailureMessage}
                        </p>
                    )}
                </div>
            )}

            {isFullyAnswered && (
                <div
                    className="rounded-xl p-4 flex flex-col gap-2"
                    style={{ background: "var(--good-soft)" }}
                >
                    <p className="text-sm" style={{ color: "var(--good)" }}>
                        {describeResolvedBatch(summary)}
                    </p>
                    {lessonsToPublish.length > 0 && (
                        <>
                            <p className="text-xs text-ink-2">
                                Правки записаны в упражнения, но команда увидит их только после
                                публикации новой версии урока:
                            </p>
                            <ul className="flex flex-col gap-1">
                                {lessonsToPublish.map((lesson) => (
                                    <li key={lesson.lessonId}>
                                        <Link
                                            href={`/org/content/lessons/${lesson.lessonId}`}
                                            className="text-sm underline text-ink-2"
                                        >
                                            {lesson.lessonTitle} · принято {lesson.acceptedCount}
                                        </Link>
                                    </li>
                                ))}
                            </ul>
                        </>
                    )}
                </div>
            )}
        </div>
    );
}
