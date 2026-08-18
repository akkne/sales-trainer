"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    AssignmentCompletionRule,
    AssignmentContentKind,
    AssignmentProgressStatus,
} from "@/features/assignments/utils/completion-rule";

export {
    describeCompletionRule,
    daysUntilDeadline,
} from "@/features/assignments/utils/completion-rule";
export type {
    AssignmentCompletionRule,
    AssignmentContentKind,
    AssignmentProgressStatus,
} from "@/features/assignments/utils/completion-rule";

export interface ActiveAssignmentItem {
    kind: AssignmentContentKind;
    reference: string;
    orderIndex: number;
    /** Null when the referenced content was archived after the assignment was issued. */
    title: string | null;
    /** Set only for `lesson_version` — what `/session/:lessonId` needs. */
    lessonId: string | null;
}

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
