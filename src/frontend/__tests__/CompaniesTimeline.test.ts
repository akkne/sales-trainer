import { describe, it, expect } from "vitest";
import { mergeTimeline, filterTimeline } from "@/features/companies/lib/timeline";
import type { PracticeCall } from "@/features/companies/hooks/use-practice-calls";
import type { CallLogEntry } from "@/features/companies/hooks/use-company-logs";

const practiceCalls: PracticeCall[] = [
    { id: "p1", companyId: "c1", dialogSessionId: "d1", goal: "Договориться о встрече", createdAt: "2026-07-05T10:00:00Z" },
    { id: "p2", companyId: "c1", dialogSessionId: "d2", goal: "", createdAt: "2026-07-08T10:00:00Z" },
];

const logs: CallLogEntry[] = [
    {
        id: "l1", companyId: "c1", contactName: "Иван", subject: "Обсудили цену", outcome: "Пришлём КП",
        occurredAt: "2026-07-06T10:00:00Z", createdAt: "2026-07-06T10:00:00Z", updatedAt: "2026-07-06T10:00:00Z",
        contactId: null,
    },
];

describe("mergeTimeline", () => {
    it("merges practice calls and logs into one reverse-chronological feed", () => {
        const entries = mergeTimeline(practiceCalls, logs);
        expect(entries).toHaveLength(3);
        expect(entries.map((e) => e.timestamp)).toEqual([
            "2026-07-08T10:00:00Z",
            "2026-07-06T10:00:00Z",
            "2026-07-05T10:00:00Z",
        ]);
    });

    it("tags each entry with its kind", () => {
        const entries = mergeTimeline(practiceCalls, logs);
        expect(entries[0].kind).toBe("practice");
        expect(entries[1].kind).toBe("reallog");
        expect(entries[2].kind).toBe("practice");
    });

    it("returns an empty feed for no data", () => {
        expect(mergeTimeline([], [])).toEqual([]);
    });
});

describe("filterTimeline", () => {
    const entries = mergeTimeline(practiceCalls, logs);

    it("'all' returns every entry unchanged", () => {
        expect(filterTimeline(entries, "all")).toHaveLength(3);
    });

    it("'practice' keeps only practice entries", () => {
        const filtered = filterTimeline(entries, "practice");
        expect(filtered).toHaveLength(2);
        expect(filtered.every((e) => e.kind === "practice")).toBe(true);
    });

    it("'reallog' keeps only real-call log entries", () => {
        const filtered = filterTimeline(entries, "reallog");
        expect(filtered).toHaveLength(1);
        expect(filtered[0].kind).toBe("reallog");
    });
});
