"use client";

import type { AssignmentSummary } from "@/features/org-assignments/types/assignment";
import { buildListFunnelSegments } from "@/features/org-assignments/utils/funnel";

interface AssignmentFunnelBarProps {
    summary: AssignmentSummary;
}

/**
 * The funnel inside a list row: four segments and «выполнили / выдано», so that «кому надо
 * позвонить» is visible without opening the card.
 *
 * The «ниже порога» line under the bar is drawn only when somebody is actually under it — it is the
 * one line of the list worth opening an assignment for, and printing a zero would bury it.
 */
export function AssignmentFunnelBar({ summary }: AssignmentFunnelBarProps) {
    const segments = buildListFunnelSegments(summary);
    const total = summary.assignedCount;

    if (total === 0) {
        return <span className="text-ink-4">—</span>;
    }

    return (
        <div className="min-w-[140px]">
            <div className="flex items-center gap-2">
                <div
                    className="flex h-2 flex-1 overflow-hidden"
                    style={{ background: "var(--bg-2)", borderRadius: "999px" }}
                    role="img"
                    aria-label={segments
                        .map((segment) => `${segment.label}: ${segment.count}`)
                        .join(", ")}
                >
                    {segments.map((segment) => (
                        <div
                            key={segment.key}
                            style={{
                                width: `${(segment.count / total) * 100}%`,
                                background: segment.color,
                            }}
                        />
                    ))}
                </div>
                <span
                    className="tnum text-xs text-ink-2"
                    style={{ fontFamily: "var(--font-mono)" }}
                >
                    {summary.completedCount}/{total}
                </span>
            </div>
            {summary.failedThresholdCount > 0 && (
                <div className="mt-1 text-xs" style={{ color: "var(--heart)" }}>
                    ▲ {summary.failedThresholdCount} ниже порога
                </div>
            )}
        </div>
    );
}
