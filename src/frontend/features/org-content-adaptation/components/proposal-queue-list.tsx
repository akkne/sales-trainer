"use client";

import { useMemo } from "react";
import {
    describeExerciseType,
    describeItemStatus,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import type { ContentAdaptationItemSummary } from "@/features/org-content-adaptation/types/adaptation";
import { groupQueueByLesson } from "@/features/org-content-adaptation/utils/proposal-queue";

/**
 * The left column of O13: the whole batch, in the order the lessons are played, so the queue reads
 * the way a manager will meet it.
 *
 * The marker in front of a row is what makes the column scannable without opening anything — and in
 * review mode a blocking finding wears `⚠` and sits at the top of its lesson.
 */

const STATUS_MARKERS: Record<string, string> = {
    pending: "·",
    proposed: "●",
    unchanged: "—",
    accepted: "✓",
    rejected: "✗",
    failed: "!",
};

const BLOCKING_MARKER = "⚠";

interface ProposalQueueListProps {
    items: readonly ContentAdaptationItemSummary[];
    mode: string;
    selectedItemId: string | null;
    onSelect: (itemId: string) => void;
}

export function ProposalQueueList({
    items,
    mode,
    selectedItemId,
    onSelect,
}: ProposalQueueListProps) {
    const lessonGroups = useMemo(() => groupQueueByLesson(items, mode), [items, mode]);

    const positionByItemId = useMemo(() => {
        const positions = new Map<string, number>();
        let position = 0;
        for (const group of lessonGroups) {
            for (const item of group.items) {
                position += 1;
                positions.set(item.id, position);
            }
        }
        return positions;
    }, [lessonGroups]);

    return (
        <nav aria-label="Очередь предложений" className="flex flex-col gap-4">
            {lessonGroups.map((group) => (
                <div key={group.lessonId} className="flex flex-col gap-1">
                    <h3 className="px-2 text-xs font-medium text-ink-3 uppercase tracking-wide">
                        {group.lessonTitle}
                    </h3>
                    {group.items.map((item) => {
                        const isSelected = item.id === selectedItemId;
                        const marker = item.hasBlockingFinding
                            ? BLOCKING_MARKER
                            : (STATUS_MARKERS[item.status] ?? "·");

                        return (
                            <button
                                key={item.id}
                                type="button"
                                onClick={() => onSelect(item.id)}
                                aria-current={isSelected}
                                className="flex items-baseline gap-2 w-full text-left px-2 py-1.5 rounded-lg transition-colors hover:bg-bg-2"
                                style={{
                                    background: isSelected ? "var(--bg-2)" : "transparent",
                                    color: isSelected ? "var(--ink)" : "var(--ink-2)",
                                }}
                            >
                                <span
                                    className="tnum text-xs text-ink-3 w-6 shrink-0"
                                    style={{ fontFamily: "var(--font-mono)" }}
                                >
                                    {positionByItemId.get(item.id)}
                                </span>
                                <span aria-hidden className="shrink-0 text-xs">
                                    {marker}
                                </span>
                                <span className="min-w-0 flex-1 text-sm truncate">
                                    {describeExerciseType(item.exerciseType)}
                                </span>
                                <span className="shrink-0 text-xs text-ink-3">
                                    {describeItemStatus(item.status)}
                                </span>
                            </button>
                        );
                    })}
                </div>
            ))}
        </nav>
    );
}
