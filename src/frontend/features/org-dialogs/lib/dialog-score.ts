import type { ChipTone } from "@/shared/components/chip";

/**
 * O5–O7 grade the same conversation on two different scales, and this file is the single place the
 * panel reconciles them.
 *
 * ai-service grades a conversation 0–10 (`DialogScoreScale`) and that is the number
 * `GET /admin/dialog-sessions` returns and the number its `maxScore` filter compares against.
 * learning-service stores the same grade multiplied by ten — `DialogService.NormalizeScoreForLearningService`
 * — and *that* is the number a dispute carries (`disputedScore`), the number a corrected score is
 * expressed in (`adjustedScore`, 0–100) and the number an assignment threshold («оценка >= 70») is
 * written against. The panel therefore shows 0–100 everywhere and converts on the ai-service edge,
 * because a РОП who sees «4» on O5 and «40» on O7 for one conversation has been handed a puzzle
 * instead of a number.
 */

/** `DialogScoreScale.LearningServiceScaleFactor`. */
export const DIALOG_SCORE_SCALE_FACTOR = 10;

export const MINIMUM_PANEL_SCORE = 0;
export const MAXIMUM_PANEL_SCORE = 100;

/** An ai-service grade as the panel shows it. Null stays null — an ungraded conversation has no zero. */
export function toPanelScore(aiServiceScore: number | null | undefined): number | null {
    if (aiServiceScore === null || aiServiceScore === undefined) return null;
    return aiServiceScore * DIALOG_SCORE_SCALE_FACTOR;
}

/**
 * The `maxScore` value to send for a ceiling the РОП picked on the panel's scale.
 *
 * Floored rather than rounded: «не выше 60» must never return a conversation graded above 60, and
 * the granularity that costs is exactly why the filter offers whole tens and nothing between them.
 */
export function toApiMaximumScore(panelMaximumScore: number): number {
    return Math.floor(panelMaximumScore / DIALOG_SCORE_SCALE_FACTOR);
}

/**
 * The ceilings the filter offers. Ten values because the underlying grade has eleven — anything
 * finer would be a control that silently rounds what the person typed.
 */
export const PANEL_SCORE_CEILING_CHOICES = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100] as const;

/**
 * §2 O5: «Фильтр "оценка не выше" — главный элемент экрана, и он преднастроен на 60». A list of
 * every conversation is not an agenda; a list of the bad ones is.
 */
export const DEFAULT_PANEL_SCORE_CEILING = 60;

/**
 * Colour for a grade on the panel's scale. The bands follow the learner-facing wording in
 * `features/dialog/components/feedback-modal.tsx` («Провал/Слабо» ≤ 4, «Нормально» ≤ 6) so the two
 * sides of the product do not disagree about whether a conversation went badly.
 */
export function resolvePanelScoreTone(panelScore: number | null): ChipTone {
    if (panelScore === null) return "neutral";
    if (panelScore <= 40) return "bad";
    if (panelScore <= 60) return "warn";
    return "good";
}

/** A grade for display, or an em dash — never «0», which reads as a grade that was given. */
export function formatPanelScore(panelScore: number | null): string {
    return panelScore === null ? "—" : String(panelScore);
}
