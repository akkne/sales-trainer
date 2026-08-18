/// Why a real gap is not being offered as a button, in the panel's fixed vocabulary
/// (ADMIN_UI_DESIGN.md §1.4). Mirrors `TeamSkillGapSuppressionReasons` in learning-service.
export const GAP_SUPPRESSION_REASONS = {
    dismissed: "dismissed",
    runInProgress: "run_in_progress",
    recentlyAddressed: "recently_addressed",
} as const;

export type GapSuppressionReason =
    (typeof GAP_SUPPRESSION_REASONS)[keyof typeof GAP_SUPPRESSION_REASONS];

const GAP_SUPPRESSION_LABELS: Record<string, string> = {
    [GAP_SUPPRESSION_REASONS.dismissed]: "Отложено вами",
    [GAP_SUPPRESSION_REASONS.runInProgress]: "Уже идёт генерация",
    [GAP_SUPPRESSION_REASONS.recentlyAddressed]: "Недавно закрывали",
};

const UNKNOWN_SUPPRESSION_LABEL = "Не предлагаем";

/// The Russian label for a suppression reason. An unrecognised reason falls back to the neutral
/// «не предлагаем» rather than to the raw key: a new reason added on the server must degrade into
/// something a РОП can read, not into `recently_expired`.
export function describeGapSuppressionReason(reason: string): string {
    return GAP_SUPPRESSION_LABELS[reason] ?? UNKNOWN_SUPPRESSION_LABEL;
}
