import {
    MAXIMUM_PANEL_SCORE,
    MINIMUM_PANEL_SCORE,
} from "@/features/org-dialogs/lib/dialog-score";
import type { ScoreDisputeOutcome } from "@/features/org-dialogs/constants/dialog-review-dictionary";
import type { ResolveScoreDisputeInput } from "@/features/org-dialogs/hooks/use-dialog-review-notes";

/**
 * The verdict form of O7, checked here rather than inside the component so the rules stay one
 * paragraph long and testable.
 *
 * They are the server's rules, restated in the caller's language: `DialogReviewService` refuses a
 * rejection without words and a corrected score on anything but an upheld dispute. Restating them
 * is not distrust of the server — it is the difference between «Оценка остаётся в силе, потому
 * что…» said before the click and a red toast said after it.
 */

export interface DisputeVerdictDraft {
    outcome: ScoreDisputeOutcome | null;
    resolution: string;
    /** Free text, because it comes from an input; parsed here and nowhere else. */
    adjustedScore: string;
}

export interface DisputeVerdictValidation {
    canSubmit: boolean;
    resolutionError: string | null;
    adjustedScoreError: string | null;
}

export const EMPTY_DISPUTE_VERDICT_DRAFT: DisputeVerdictDraft = {
    outcome: null,
    resolution: "",
    adjustedScore: "",
};

function parseAdjustedScore(rawAdjustedScore: string): number | null | "invalid" {
    const trimmed = rawAdjustedScore.trim();
    if (trimmed.length === 0) return null;
    if (!/^\d{1,3}$/.test(trimmed)) return "invalid";

    const parsed = Number(trimmed);
    if (parsed < MINIMUM_PANEL_SCORE || parsed > MAXIMUM_PANEL_SCORE) return "invalid";
    return parsed;
}

export function validateDisputeVerdict(draft: DisputeVerdictDraft): DisputeVerdictValidation {
    if (draft.outcome === null) {
        return { canSubmit: false, resolutionError: null, adjustedScoreError: null };
    }

    const resolutionError =
        draft.outcome === "rejected" && draft.resolution.trim().length === 0
            ? "Оценка остаётся в силе, потому что… — напишите менеджеру, почему."
            : null;

    const parsedAdjustedScore =
        draft.outcome === "upheld" ? parseAdjustedScore(draft.adjustedScore) : null;
    const adjustedScoreError =
        parsedAdjustedScore === "invalid" ? "Оценка — целое число от 0 до 100." : null;

    return {
        canSubmit: resolutionError === null && adjustedScoreError === null,
        resolutionError,
        adjustedScoreError,
    };
}

/**
 * The request body, or null when the draft is not ready. `resolution` is sent even on an upheld
 * dispute if the РОП wrote one — agreement needs no defence, but it is allowed to have words.
 */
export function buildResolveDisputeRequest(
    noteId: string,
    draft: DisputeVerdictDraft
): ResolveScoreDisputeInput | null {
    if (draft.outcome === null || !validateDisputeVerdict(draft).canSubmit) return null;

    const trimmedResolution = draft.resolution.trim();
    const parsedAdjustedScore =
        draft.outcome === "upheld" ? parseAdjustedScore(draft.adjustedScore) : null;

    return {
        noteId,
        outcome: draft.outcome,
        resolution: trimmedResolution.length > 0 ? trimmedResolution : null,
        adjustedScore: typeof parsedAdjustedScore === "number" ? parsedAdjustedScore : null,
    };
}
