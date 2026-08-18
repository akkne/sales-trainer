import type {
    AssignmentFunnel,
    AssignmentSummary,
} from "@/features/org-assignments/types/assignment";

export interface FunnelSegment {
    key: string;
    label: string;
    count: number;
    color: string;
}

export interface FunnelStage extends FunnelSegment {
    /** Share of the assigned population, 0–100, for the stage's own bar. */
    filledPercent: number;
}

/**
 * The four slices of the list row's micro-bar, in the order the design fixes: finished, under the
 * bar, still working, never opened.
 *
 * `AssignmentSummaryDto` has no `notStartedCount` — only the dashboard's `AssignmentFunnelDto` does
 * — so the row derives it as `assignedCount − startedCount`, and «в работе» as the started who have
 * neither finished nor failed. Both are clamped at zero: the counts come from separate aggregates
 * and a momentarily inconsistent read must not draw a negative segment.
 */
export function buildListFunnelSegments(summary: AssignmentSummary): FunnelSegment[] {
    const inProgressCount = Math.max(
        0,
        summary.startedCount - summary.completedCount - summary.failedThresholdCount
    );
    const notStartedCount = Math.max(0, summary.assignedCount - summary.startedCount);

    return [
        {
            key: "completed",
            label: "Выполнили",
            count: summary.completedCount,
            color: "var(--success)",
        },
        {
            key: "failed_threshold",
            label: "Ниже порога",
            count: summary.failedThresholdCount,
            color: "var(--heart)",
        },
        { key: "in_progress", label: "В работе", count: inProgressCount, color: "var(--amber)" },
        {
            key: "not_started",
            label: "Не начали",
            count: notStartedCount,
            color: "var(--line-strong)",
        },
    ];
}

/**
 * The five stages of O4. `failed_threshold` is a column of its own and never a slice of «выполнили»
 * — a four-stage funnel puts the people who tried and missed the bar back among the people who
 * never opened it, which is the exact confusion 40.22 separated the two states to end.
 */
export function buildDashboardFunnelStages(funnel: AssignmentFunnel): FunnelStage[] {
    const denominator = funnel.assignedCount > 0 ? funnel.assignedCount : 0;
    const toPercent = (count: number) =>
        denominator === 0 ? 0 : Math.min(100, Math.round((count / denominator) * 100));

    const stages: FunnelSegment[] = [
        { key: "assigned", label: "Выдано", count: funnel.assignedCount, color: "var(--ink-3)" },
        {
            key: "not_started",
            label: "Не начали",
            count: funnel.notStartedCount,
            color: "var(--line-strong)",
        },
        { key: "started", label: "Начали", count: funnel.startedCount, color: "var(--amber)" },
        {
            key: "completed",
            label: "Выполнили",
            count: funnel.completedCount,
            color: "var(--success)",
        },
        {
            key: "failed_threshold",
            label: "Ниже порога",
            count: funnel.failedThresholdCount,
            color: "var(--heart)",
        },
    ];

    return stages.map((stage) => ({ ...stage, filledPercent: toPercent(stage.count) }));
}

/**
 * How many people a reminder of this scope would reach, taken from the funnel rather than counted
 * again: the button's number and the funnel's number disagreeing is worse than either being stale.
 */
export function countReminderRecipients(
    funnel: AssignmentFunnel,
    scope: "not_started" | "unfinished"
): number {
    if (scope === "not_started") return funnel.notStartedCount;

    return Math.max(0, funnel.assignedCount - funnel.completedCount);
}

export interface DeadlineDescription {
    text: string;
    /**
     * A deadline that has passed on a running assignment. Amber, never red: 40.26 deliberately does
     * not close assignments on a timer, so this is a normal state rather than an emergency.
     */
    isOverdue: boolean;
}

function formatShortDate(isoDate: string): string {
    const parsedDate = new Date(isoDate);
    if (Number.isNaN(parsedDate.getTime())) return isoDate;

    return new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "short" }).format(parsedDate);
}

export function formatLongDate(isoDate: string | null): string {
    if (!isoDate) return "—";

    const parsedDate = new Date(isoDate);
    if (Number.isNaN(parsedDate.getTime())) return isoDate;

    return new Intl.DateTimeFormat("ru-RU", {
        day: "numeric",
        month: "long",
        year: "numeric",
    }).format(parsedDate);
}

/** The deadline cell: «через N дн» / «сегодня» / «прошёл 11 авг.» / «—». */
export function describeDeadline(
    deadline: string | null,
    now: Date = new Date()
): DeadlineDescription {
    if (!deadline) return { text: "—", isOverdue: false };

    const due = new Date(deadline).getTime();
    if (Number.isNaN(due)) return { text: "—", isOverdue: false };

    const wholeDaysLeft = Math.floor((due - now.getTime()) / 86_400_000);

    if (wholeDaysLeft < 0) return { text: `прошёл ${formatShortDate(deadline)}`, isOverdue: true };
    if (wholeDaysLeft === 0) return { text: "сегодня", isOverdue: false };

    return { text: `через ${wholeDaysLeft} дн`, isOverdue: false };
}

/** «Волна 1: 6 из 12 · Волна 2: 9 из 12» — the comparison 40.24 made separate waves for. */
export function describeWaveComparison(
    waves: { waveIndex: number; funnel: AssignmentFunnel }[]
): string | null {
    if (waves.length < 2) return null;

    return waves
        .map(
            (wave) =>
                `Волна ${wave.waveIndex + 1}: ${wave.funnel.completedCount} из ${wave.funnel.assignedCount}`
        )
        .join(" · ");
}
