import type {
    AssignmentCompletionRule,
    AssignmentContentKind,
} from "@/features/assignments/utils/completion-rule";

export type CompletionRuleDraftKind = "none" | "dialog_score" | "exercise_accuracy";

/**
 * What the threshold editor holds while it is being filled in.
 *
 * `none` is a real state and the initial one: 40.22 refuses an assignment without a threshold, and
 * a form that pre-selected a rule would hand the РОП a bar they never chose (§O3 — «Без порога
 * задание засчитывалось бы за клик»).
 */
export interface CompletionRuleDraft {
    kind: CompletionRuleDraftKind;
    minimumScore: number;
    requiredCount: number;
    minimumAccuracyPercent: number;
}

/** The server's own bounds, mirrored so the screen refuses before the request does. */
export const COMPLETION_RULE_LIMITS = {
    minimumScore: { minimum: 1, maximum: 100 },
    requiredCount: { minimum: 1, maximum: 20 },
    minimumAccuracyPercent: { minimum: 1, maximum: 100 },
} as const;

export const EMPTY_COMPLETION_RULE_DRAFT: CompletionRuleDraft = {
    kind: "none",
    minimumScore: 70,
    requiredCount: 3,
    minimumAccuracyPercent: 80,
};

/**
 * The content kind a rule is measured over. `dialog_score` counts conversations, `exercise_accuracy`
 * counts exercises — an assignment carrying neither is refused by `activate` with a 409, and this is
 * how the screen refuses first.
 */
export function requiredContentKindForRule(
    kind: CompletionRuleDraftKind
): AssignmentContentKind | null {
    if (kind === "dialog_score") return "dialog_scenario";
    if (kind === "exercise_accuracy") return "lesson_version";
    return null;
}

function isWholeNumberWithin(value: number, bounds: { minimum: number; maximum: number }): boolean {
    return Number.isInteger(value) && value >= bounds.minimum && value <= bounds.maximum;
}

/**
 * The refusal to show under the threshold section, or null when the draft can be sent.
 *
 * It checks the numbers and the content in one place because both produce the same user-visible
 * outcome — an assignment nobody can complete — and the РОП should read one sentence, not two.
 */
export function validateCompletionRuleDraft(
    draft: CompletionRuleDraft,
    contentKinds: AssignmentContentKind[]
): string | null {
    if (draft.kind === "none") {
        return "Выберите, что считается выполнением. Без порога задание засчитывалось бы за клик.";
    }

    if (draft.kind === "dialog_score") {
        if (!isWholeNumberWithin(draft.requiredCount, COMPLETION_RULE_LIMITS.requiredCount)) {
            return `Количество разговоров — целое число от ${COMPLETION_RULE_LIMITS.requiredCount.minimum} до ${COMPLETION_RULE_LIMITS.requiredCount.maximum}.`;
        }
        if (!isWholeNumberWithin(draft.minimumScore, COMPLETION_RULE_LIMITS.minimumScore)) {
            return `Оценка — целое число от ${COMPLETION_RULE_LIMITS.minimumScore.minimum} до ${COMPLETION_RULE_LIMITS.minimumScore.maximum}.`;
        }
    }

    if (
        draft.kind === "exercise_accuracy" &&
        !isWholeNumberWithin(
            draft.minimumAccuracyPercent,
            COMPLETION_RULE_LIMITS.minimumAccuracyPercent
        )
    ) {
        return `Точность — целое число процентов от ${COMPLETION_RULE_LIMITS.minimumAccuracyPercent.minimum} до ${COMPLETION_RULE_LIMITS.minimumAccuracyPercent.maximum}.`;
    }

    const requiredKind = requiredContentKindForRule(draft.kind);
    if (requiredKind !== null && !contentKinds.includes(requiredKind)) {
        return requiredKind === "dialog_scenario"
            ? "Этот порог измеряется по разговорам, а в задании их нет. Добавьте разговор или выберите другой порог."
            : "Этот порог измеряется по упражнениям, а в задании их нет. Добавьте упражнения из урока или выберите другой порог.";
    }

    return null;
}

/** The document the API stores, or null while the draft is not a rule yet. */
export function buildCompletionRuleDocument(
    draft: CompletionRuleDraft
): AssignmentCompletionRule | null {
    if (draft.kind === "dialog_score") {
        return {
            kind: "dialog_score",
            minimumScore: draft.minimumScore,
            requiredCount: draft.requiredCount,
        };
    }

    if (draft.kind === "exercise_accuracy") {
        return {
            kind: "exercise_accuracy",
            minimumAccuracyPercent: draft.minimumAccuracyPercent,
        };
    }

    return null;
}

/**
 * A stored rule read back into the editor. An unrecognised kind lands on `none` rather than on a
 * fabricated one — the same refusal to guess `describeCompletionRule` makes when rendering it.
 */
export function toCompletionRuleDraft(
    rule: AssignmentCompletionRule | null | undefined
): CompletionRuleDraft {
    if (rule && rule.kind === "dialog_score" && "minimumScore" in rule && "requiredCount" in rule) {
        return {
            ...EMPTY_COMPLETION_RULE_DRAFT,
            kind: "dialog_score",
            minimumScore: rule.minimumScore,
            requiredCount: rule.requiredCount,
        };
    }

    if (rule && rule.kind === "exercise_accuracy" && "minimumAccuracyPercent" in rule) {
        return {
            ...EMPTY_COMPLETION_RULE_DRAFT,
            kind: "exercise_accuracy",
            minimumAccuracyPercent: rule.minimumAccuracyPercent,
        };
    }

    return EMPTY_COMPLETION_RULE_DRAFT;
}

/**
 * The warning `docs/DONT_FORGET.md` asks for: an assignment holding both exercises and a
 * conversation is measured on exactly one of the two, and a РОП who is not told this believes the
 * threshold requires both.
 */
export function describeHalfMeasuredContentWarning(
    contentKinds: AssignmentContentKind[],
    ruleKind: CompletionRuleDraftKind
): string | null {
    const hasBothHalves =
        contentKinds.includes("lesson_version") && contentKinds.includes("dialog_scenario");
    if (!hasBothHalves || ruleKind === "none") return null;

    const measuredHalf = ruleKind === "dialog_score" ? "разговоры" : "упражнения";
    const unmeasuredHalf = ruleKind === "dialog_score" ? "Упражнения" : "Разговор";

    return `В задании есть и упражнения, и разговор, но порог измеряет только ${measuredHalf}. ${unmeasuredHalf} останется полезной практикой и на выполнение не повлияет.`;
}

/** «лучшая 61» for a dialog score, «точность 61%» for exercise accuracy — the two are not one label. */
export function describeBestScore(
    bestScore: number | null,
    ruleKind: string | null | undefined
): string {
    if (bestScore === null) return "—";
    if (ruleKind === "exercise_accuracy") return `точность ${bestScore}%`;
    return `лучшая ${bestScore}`;
}
