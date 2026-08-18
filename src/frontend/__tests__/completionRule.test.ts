import { describe, expect, it } from "vitest";
import {
    daysUntilDeadline,
    describeCompletionRule,
    type AssignmentCompletionRule,
} from "@/features/assignments/utils/completion-rule";

/**
 * Both helpers moved out of `hooks/use-assignments.ts` in block 40.20 so the organization panel
 * can import them without pulling React Query along. The behaviour they must keep is that an
 * unknown rule renders as nothing: 40.21 left `completion_rule` open on purpose, and an old client
 * guessing at a new kind would put a wrong number in front of a manager.
 */
describe("describeCompletionRule", () => {
    it("spells out a dialog-score rule", () => {
        const rule: AssignmentCompletionRule = {
            kind: "dialog_score",
            minimumScore: 70,
            requiredCount: 3,
        };
        expect(describeCompletionRule(rule)).toBe("3 разговора с оценкой не ниже 70");
    });

    it("declines «разговор» the way Russian does", () => {
        const withCount = (requiredCount: number): string | null =>
            describeCompletionRule({ kind: "dialog_score", minimumScore: 60, requiredCount });

        expect(withCount(1)).toContain("1 разговор ");
        expect(withCount(2)).toContain("2 разговора ");
        expect(withCount(5)).toContain("5 разговоров ");
        expect(withCount(11)).toContain("11 разговоров ");
        expect(withCount(21)).toContain("21 разговор ");
        expect(withCount(22)).toContain("22 разговора ");
        expect(withCount(114)).toContain("114 разговоров ");
    });

    it("spells out an exercise-accuracy rule", () => {
        expect(
            describeCompletionRule({ kind: "exercise_accuracy", minimumAccuracyPercent: 80 })
        ).toBe("точность по упражнениям не ниже 80%");
    });

    it("says nothing about a rule kind this client does not know", () => {
        expect(describeCompletionRule({ kind: "voice_streak_2027" })).toBeNull();
        expect(describeCompletionRule(null)).toBeNull();
        expect(describeCompletionRule(undefined)).toBeNull();
    });

    it("says nothing about a known kind that arrived without its numbers", () => {
        expect(
            describeCompletionRule({ kind: "dialog_score" } as AssignmentCompletionRule)
        ).toBeNull();
        expect(
            describeCompletionRule({ kind: "exercise_accuracy" } as AssignmentCompletionRule)
        ).toBeNull();
    });
});

describe("daysUntilDeadline", () => {
    const now = new Date("2026-08-18T12:00:00.000Z");

    it("counts whole days left", () => {
        expect(daysUntilDeadline("2026-08-21T12:00:00.000Z", now)).toBe(3);
        expect(daysUntilDeadline("2026-08-18T23:59:00.000Z", now)).toBe(0);
    });

    it("goes negative once the deadline has passed", () => {
        expect(daysUntilDeadline("2026-08-16T12:00:00.000Z", now)).toBe(-2);
    });

    it("returns null when there is no deadline or it cannot be parsed", () => {
        expect(daysUntilDeadline(null, now)).toBeNull();
        expect(daysUntilDeadline("not a date", now)).toBeNull();
    });
});
