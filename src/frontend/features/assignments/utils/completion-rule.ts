/** Phase 40.23. Mirrors learning-service's `AssignmentProgressStatuses`. */
export type AssignmentProgressStatus =
    | "not_started"
    | "in_progress"
    | "completed"
    | "failed_threshold";

/** Phase 40.23. Mirrors learning-service's `AssignmentContentItemKinds`. */
export type AssignmentContentKind = "lesson_version" | "dialog_scenario" | "reference_material";

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

/**
 * Phase 40.23. The bar, in the words a manager can aim at.
 *
 * Returns null for a rule this client does not know, which is deliberate: 40.21 made
 * `completion_rule` an open discriminated object precisely so later blocks could add kinds, and a
 * client that guesses at an unknown one would put a wrong number on the screen.
 *
 * Lifted out of `hooks/use-assignments.ts` for block 40.20: the organization panel writes these
 * rules on the assignment-creation screen and reads them back on four others, and importing a
 * plain function out of a hook module would drag React Query into every one of them.
 */
export function describeCompletionRule(
    rule: AssignmentCompletionRule | null | undefined
): string | null {
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
