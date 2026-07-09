import type { PracticeCall } from "@/features/companies/hooks/use-practice-calls";
import type { CallLogEntry } from "@/features/companies/hooks/use-company-logs";

export type TimelineFilter = "all" | "practice" | "reallog";

export type TimelineEntry =
    | { kind: "practice"; timestamp: string; practiceCall: PracticeCall }
    | { kind: "reallog"; timestamp: string; log: CallLogEntry };

/**
 * Merges practice calls and real-call logs into one reverse-chronological
 * feed (§3.4 of the design spec). Practice calls are ordered by `createdAt`;
 * real-call logs by the user-entered `occurredAt`.
 */
export function mergeTimeline(practiceCalls: PracticeCall[], logs: CallLogEntry[]): TimelineEntry[] {
    const practiceEntries: TimelineEntry[] = practiceCalls.map((practiceCall) => ({
        kind: "practice",
        timestamp: practiceCall.createdAt,
        practiceCall,
    }));
    const logEntries: TimelineEntry[] = logs.map((log) => ({
        kind: "reallog",
        timestamp: log.occurredAt,
        log,
    }));

    return [...practiceEntries, ...logEntries].sort(
        (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
    );
}

/** Applies the segmented filter (Все / Тренировки / Звонки) to a merged timeline. */
export function filterTimeline(entries: TimelineEntry[], filter: TimelineFilter): TimelineEntry[] {
    if (filter === "all") return entries;
    return entries.filter((entry) => entry.kind === filter);
}
