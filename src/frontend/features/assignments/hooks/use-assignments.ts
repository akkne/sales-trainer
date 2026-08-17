"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

/** Phase 40.23. Mirrors learning-service's `AssignmentProgressStatuses`. */
export type AssignmentProgressStatus =
    | "not_started"
    | "in_progress"
    | "completed"
    | "failed_threshold";

/** Phase 40.23. Mirrors learning-service's `AssignmentContentItemKinds`. */
export type AssignmentContentKind = "lesson_version" | "dialog_scenario" | "reference_material";

export interface ActiveAssignmentItem {
    kind: AssignmentContentKind;
    reference: string;
    orderIndex: number;
    /** Null when the referenced content was archived after the assignment was issued. */
    title: string | null;
    /** Set only for `lesson_version` — what `/session/:lessonId` needs. */
    lessonId: string | null;
}

/**
 * Phase 40.23. The completion rule, verbatim from the server.
 *
 * Typed as a discriminated union rather than `unknown` because the card's whole reason for
 * showing it is that a manager who cannot see the bar cannot aim at it. An unrecognised `kind`
 * renders as nothing rather than as a guess — a future 40.24/40.25 rule must not display as a
 * wrong sentence in an old client.
 */
export type AssignmentCompletionRule =
    | { kind: "dialog_score"; minimumScore: number; requiredCount: number }
    | { kind: "exercise_accuracy"; minimumAccuracyPercent: number }
    | { kind: string };

export interface ActiveAssignment {
    id: string;
    title: string;
    goal: string | null;
    opensAt: string | null;
    deadline: string | null;
    completionRule: AssignmentCompletionRule;
    content: ActiveAssignmentItem[];
    status: AssignmentProgressStatus;
    bestScore: number | null;
    attemptCount: number;
    firstOpenedAt: string | null;
    completedAt: string | null;
}

/**
 * Phase 40.23. The caller's own unfinished assignments, soonest deadline first.
 *
 * An empty array is the normal case and the card renders nothing for it: the learning path is the
 * home screen, and an assignment strip is an addition to it rather than a replacement.
 */
export function useActiveAssignments() {
    return useQuery<ActiveAssignment[]>({
        queryKey: ["assignments", "active"],
        queryFn: () => apiClient.get<ActiveAssignment[]>("/assignments/active"),
        // Progress moves on Kafka events a moment after the work (40.22), never in the response to
        // the submit itself, so a fresh-on-focus read is what makes the strip catch up after
        // somebody finishes a lesson in another tab.
        staleTime: 30_000,
    });
}

/**
 * Phase 40.23. The bar, in the words a manager can aim at.
 *
 * Returns null for a rule this client does not know, which is deliberate: 40.21 made
 * `completion_rule` an open discriminated object precisely so later blocks could add kinds, and a
 * client that guesses at an unknown one would put a wrong number on the screen.
 */
export function describeCompletionRule(rule: AssignmentCompletionRule | null | undefined): string | null {
    if (!rule) return null;

    if (rule.kind === "dialog_score" && "requiredCount" in rule && "minimumScore" in rule) {
        return `${rule.requiredCount} ${pluralConversations(rule.requiredCount)} с оценкой не ниже ${rule.minimumScore}`;
    }

    if (rule.kind === "exercise_accuracy" && "minimumAccuracyPercent" in rule) {
        return `точность по упражнениям не ниже ${rule.minimumAccuracyPercent}%`;
    }

    return null;
}

function pluralConversations(count: number): string {
    const lastTwo = count % 100;
    const last = count % 10;
    if (lastTwo >= 11 && lastTwo <= 14) return "разговоров";
    if (last === 1) return "разговор";
    if (last >= 2 && last <= 4) return "разговора";
    return "разговоров";
}

/** Whole days until the deadline; negative once it has passed, null when there is none. */
export function daysUntilDeadline(deadline: string | null, now: Date = new Date()): number | null {
    if (!deadline) return null;

    const due = new Date(deadline).getTime();
    if (Number.isNaN(due)) return null;

    return Math.floor((due - now.getTime()) / 86_400_000);
}
