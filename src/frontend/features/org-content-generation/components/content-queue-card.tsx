"use client";

import Link from "next/link";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Skeleton } from "@/shared/components/skeleton";
import type { QueueCardCopy } from "@/features/org-content-generation/utils/queue-copy";

interface ContentQueueCardProps {
    title: string;
    copy: QueueCardCopy;
    actionLabel: string;
    actionHref: string;
    isLoading: boolean;
    /** True when this queue's own count could not be read — the card stays, the numbers go. */
    hasCountFailure: boolean;
}

const COUNT_UNAVAILABLE_LABEL = "Не удалось прочитать очередь — цифры сейчас неизвестны.";

/**
 * One of O9's three queues.
 *
 * An empty queue explains the section instead of printing a zero: «0 ждёт проверки» tells a РОП
 * opening this page for the first time nothing at all, and the sentence about what would appear
 * there is the entire value of the card. A queue whose count failed to load says exactly that,
 * rather than showing a zero it did not measure.
 */
export function ContentQueueCard({
    title,
    copy,
    actionLabel,
    actionHref,
    isLoading,
    hasCountFailure,
}: ContentQueueCardProps) {
    return (
        <Card padding={20} className="flex flex-col">
            <h2 className="text-xs font-medium uppercase tracking-wide text-ink-3">{title}</h2>

            <div className="mt-3 flex-1">
                {isLoading && <Skeleton height={44} rounded={10} />}

                {!isLoading && hasCountFailure && (
                    <p className="text-sm text-ink-3">{COUNT_UNAVAILABLE_LABEL}</p>
                )}

                {!isLoading && !hasCountFailure && copy.lines.length > 0 && (
                    <ul className="flex flex-col gap-1">
                        {copy.lines.map((line) => (
                            <li key={line} className="mono text-sm text-ink">
                                {line}
                            </li>
                        ))}
                    </ul>
                )}

                {!isLoading && !hasCountFailure && copy.lines.length === 0 && (
                    <p className="text-sm text-ink-3">{copy.emptyDescription}</p>
                )}
            </div>

            <div className="mt-5">
                <Link href={actionHref}>
                    <Button variant="outline" size="md" fullWidth>
                        {actionLabel}
                    </Button>
                </Link>
            </div>
        </Card>
    );
}
