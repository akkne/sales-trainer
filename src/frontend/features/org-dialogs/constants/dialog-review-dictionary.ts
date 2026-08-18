import type { ChipTone } from "@/shared/components/chip";

/**
 * O5–O7 (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4, §2). Every backend value these three screens put in
 * front of a human is translated exactly once, here. A literal in JSX is how the same
 * `score_dispute` becomes «спор» on one screen and «оспаривание» on the next.
 */

/** Mirrors `Sellevate.Learning.Common.Constants.DialogReviewKinds`. */
export type DialogReviewKind = "coaching_note" | "score_dispute";

/** Mirrors `Sellevate.Learning.Common.Constants.DialogReviewStatuses`. */
export type DialogReviewStatus = "open" | "acknowledged" | "upheld" | "rejected";

/** The two values `ResolveScoreDisputeRequestDto.Outcome` accepts — no third one exists. */
export type ScoreDisputeOutcome = "upheld" | "rejected";

export const DIALOG_REVIEW_KIND_LABELS: Record<DialogReviewKind, string> = {
    coaching_note: "Заметка РОПа",
    score_dispute: "Оспаривание оценки",
};

/** Falls back to the raw value: a kind this build does not know is still a real row on the server. */
export function describeDialogReviewKind(kind: string): string {
    return DIALOG_REVIEW_KIND_LABELS[kind as DialogReviewKind] ?? kind;
}

export const DIALOG_REVIEW_STATUS_LABELS: Record<DialogReviewStatus, string> = {
    open: "Открыт",
    acknowledged: "Прочитано",
    upheld: "Согласились",
    rejected: "Оценка оставлена",
};

export function describeDialogReviewStatus(status: string): string {
    return DIALOG_REVIEW_STATUS_LABELS[status as DialogReviewStatus] ?? status;
}

const DIALOG_REVIEW_STATUS_TONES: Record<DialogReviewStatus, ChipTone> = {
    open: "warn",
    acknowledged: "neutral",
    upheld: "good",
    rejected: "neutral",
};

/**
 * «Оценка оставлена» is deliberately not a red chip. A rejected dispute is a decision that was
 * made, not a failure, and colouring it as a failure would tell the manager reading the same row
 * that their complaint was an error.
 */
export function resolveDialogReviewStatusTone(status: string): ChipTone {
    return DIALOG_REVIEW_STATUS_TONES[status as DialogReviewStatus] ?? "neutral";
}

/** The verdict as a *verb the РОП presses*, which is not the same wording as the status it writes. */
export const SCORE_DISPUTE_OUTCOME_LABELS: Record<ScoreDisputeOutcome, string> = {
    upheld: "Согласиться",
    rejected: "Оставить оценку",
};

/**
 * Mandatory caption under «Справедливая оценка» (O7). Without it the РОП believes they corrected
 * the grade — the backend records the number and never applies it (ASSIGNMENTS.md §4.1), and the
 * belief is discovered on the first complaint instead of here.
 */
export const ADJUSTED_SCORE_DISCLAIMER =
    "Число записывается в историю разбора и не меняет оценку разговора и не влияет на выполнение задания.";

/** Shown instead of a name for a person the team directory does not know (ADMIN_UI_DESIGN.md O5). */
export const UNNAMED_MEMBER_LABEL = "Без имени";

/** How the current user is named inside a note thread, on either side of it. */
export const CURRENT_USER_LABEL = "Вы";
