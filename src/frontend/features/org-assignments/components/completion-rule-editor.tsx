"use client";

import type { AssignmentContentKind } from "@/features/assignments/utils/completion-rule";
import {
    COMPLETION_RULE_LIMITS,
    describeHalfMeasuredContentWarning,
    type CompletionRuleDraft,
    type CompletionRuleDraftKind,
} from "@/features/org-assignments/utils/completion-rule-draft";

interface CompletionRuleEditorProps {
    draft: CompletionRuleDraft;
    contentKinds: AssignmentContentKind[];
    onChange: (draft: CompletionRuleDraft) => void;
    disabled?: boolean;
    error?: string | null;
}

/**
 * The threshold, which is the whole point of an assignment (docs/TENANCY/ASSIGNMENTS.md §1.1):
 * completion is a quality bar, not attendance.
 *
 * Neither option is selected to begin with, and a rule whose content the assignment does not carry
 * cannot be selected at all — `activate` answers 409 for exactly that, and this is the last moment
 * the two can still be reconciled by the person who can fix them.
 */
export function CompletionRuleEditor({
    draft,
    contentKinds,
    onChange,
    disabled = false,
    error = null,
}: CompletionRuleEditorProps) {
    const hasDialogContent = contentKinds.includes("dialog_scenario");
    const hasLessonContent = contentKinds.includes("lesson_version");
    const warning = describeHalfMeasuredContentWarning(contentKinds, draft.kind);

    const selectKind = (kind: CompletionRuleDraftKind) => onChange({ ...draft, kind });

    return (
        <div className="flex flex-col gap-3">
            <label
                className={`flex flex-wrap items-center gap-2 text-sm ${hasDialogContent ? "text-ink-2" : "text-ink-4"}`}
            >
                <input
                    type="radio"
                    name="completion-rule-kind"
                    checked={draft.kind === "dialog_score"}
                    disabled={disabled || !hasDialogContent}
                    onChange={() => selectKind("dialog_score")}
                />
                <span className="font-medium">Разговоры</span>
                <input
                    type="number"
                    className="w-16 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-ink"
                    aria-label="Сколько разговоров"
                    min={COMPLETION_RULE_LIMITS.requiredCount.minimum}
                    max={COMPLETION_RULE_LIMITS.requiredCount.maximum}
                    value={draft.requiredCount}
                    disabled={disabled || draft.kind !== "dialog_score"}
                    onChange={(changeEvent) =>
                        onChange({ ...draft, requiredCount: Number(changeEvent.target.value) })
                    }
                />
                <span>разговора с оценкой не ниже</span>
                <input
                    type="number"
                    className="w-16 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-ink"
                    aria-label="Минимальная оценка разговора"
                    min={COMPLETION_RULE_LIMITS.minimumScore.minimum}
                    max={COMPLETION_RULE_LIMITS.minimumScore.maximum}
                    value={draft.minimumScore}
                    disabled={disabled || draft.kind !== "dialog_score"}
                    onChange={(changeEvent) =>
                        onChange({ ...draft, minimumScore: Number(changeEvent.target.value) })
                    }
                />
            </label>
            {!hasDialogContent && (
                <p className="pl-6 text-xs text-ink-4">
                    Добавьте разговор в шаге «Что делать», чтобы измерять выполнение по нему.
                </p>
            )}

            <label
                className={`flex flex-wrap items-center gap-2 text-sm ${hasLessonContent ? "text-ink-2" : "text-ink-4"}`}
            >
                <input
                    type="radio"
                    name="completion-rule-kind"
                    checked={draft.kind === "exercise_accuracy"}
                    disabled={disabled || !hasLessonContent}
                    onChange={() => selectKind("exercise_accuracy")}
                />
                <span className="font-medium">Упражнения</span>
                <span>точность по набору не ниже</span>
                <input
                    type="number"
                    className="w-16 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-ink"
                    aria-label="Минимальная точность в процентах"
                    min={COMPLETION_RULE_LIMITS.minimumAccuracyPercent.minimum}
                    max={COMPLETION_RULE_LIMITS.minimumAccuracyPercent.maximum}
                    value={draft.minimumAccuracyPercent}
                    disabled={disabled || draft.kind !== "exercise_accuracy"}
                    onChange={(changeEvent) =>
                        onChange({
                            ...draft,
                            minimumAccuracyPercent: Number(changeEvent.target.value),
                        })
                    }
                />
                <span>%</span>
            </label>
            {!hasLessonContent && (
                <p className="pl-6 text-xs text-ink-4">
                    Добавьте упражнения из урока в шаге «Что делать», чтобы измерять выполнение по
                    ним.
                </p>
            )}

            {warning && (
                <p
                    className="rounded-lg px-3 py-2 text-xs"
                    style={{ background: "var(--amber-soft)", color: "var(--amber)" }}
                >
                    ⚠ {warning}
                </p>
            )}

            {error && (
                <p className="text-xs" style={{ color: "var(--heart)" }} role="alert">
                    {error}
                </p>
            )}
        </div>
    );
}
