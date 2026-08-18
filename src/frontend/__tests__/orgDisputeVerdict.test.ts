import { describe, expect, it } from "vitest";
import {
    buildResolveDisputeRequest,
    EMPTY_DISPUTE_VERDICT_DRAFT,
    validateDisputeVerdict,
    type DisputeVerdictDraft,
} from "@/features/org-dialogs/lib/dispute-verdict";

const NOTE_ID = "2f1d8c7b-9999-4444-8888-aaaabbbbcccc";

function draft(overrides: Partial<DisputeVerdictDraft>): DisputeVerdictDraft {
    return { ...EMPTY_DISPUTE_VERDICT_DRAFT, ...overrides };
}

/**
 * Block 40.20, screen O7. These are `DialogReviewService`'s own rules restated on the screen. The
 * one that matters is the asymmetry: closing somebody's complaint in silence is how the mechanism
 * becomes a rubber stamp, so a rejection needs words and an agreement does not.
 */
describe("the verdict on a disputed score", () => {
    it("cannot be given before an outcome is picked", () => {
        expect(validateDisputeVerdict(EMPTY_DISPUTE_VERDICT_DRAFT).canSubmit).toBe(false);
        expect(buildResolveDisputeRequest(NOTE_ID, EMPTY_DISPUTE_VERDICT_DRAFT)).toBeNull();
    });

    it("refuses to leave a grade standing without saying why", () => {
        const validation = validateDisputeVerdict(draft({ outcome: "rejected" }));
        expect(validation.canSubmit).toBe(false);
        expect(validation.resolutionError).toContain("Оценка остаётся в силе");
    });

    it("accepts a rejection once it carries a reason", () => {
        const rejection = draft({
            outcome: "rejected",
            resolution: "  Скидка действительно была отдана до выяснения объёма.  ",
        });
        expect(validateDisputeVerdict(rejection).canSubmit).toBe(true);
        expect(buildResolveDisputeRequest(NOTE_ID, rejection)).toEqual({
            noteId: NOTE_ID,
            outcome: "rejected",
            resolution: "Скидка действительно была отдана до выяснения объёма.",
            adjustedScore: null,
        });
    });

    it("asks nothing of an agreement", () => {
        expect(validateDisputeVerdict(draft({ outcome: "upheld" })).canSubmit).toBe(true);
        expect(buildResolveDisputeRequest(NOTE_ID, draft({ outcome: "upheld" }))).toEqual({
            noteId: NOTE_ID,
            outcome: "upheld",
            resolution: null,
            adjustedScore: null,
        });
    });

    it("records a corrected score on an agreement", () => {
        const agreement = draft({ outcome: "upheld", adjustedScore: "62" });
        expect(validateDisputeVerdict(agreement).canSubmit).toBe(true);
        expect(buildResolveDisputeRequest(NOTE_ID, agreement)?.adjustedScore).toBe(62);
    });

    it("takes 0 as a corrected score, because 0 is a grade somebody could deserve", () => {
        expect(buildResolveDisputeRequest(NOTE_ID, draft({ outcome: "upheld", adjustedScore: "0" }))
            ?.adjustedScore).toBe(0);
    });

    it("refuses a corrected score outside 0–100 or spelled as anything but a whole number", () => {
        for (const adjustedScore of ["101", "-5", "62.5", "шестьдесят", "6 2"]) {
            const validation = validateDisputeVerdict(draft({ outcome: "upheld", adjustedScore }));
            expect(validation.canSubmit, adjustedScore).toBe(false);
            expect(validation.adjustedScoreError, adjustedScore).toBe(
                "Оценка — целое число от 0 до 100."
            );
        }
    });

    it("never sends a corrected score with a rejection — the server refuses that pair", () => {
        const rejectionWithScore = draft({
            outcome: "rejected",
            resolution: "Оценка выставлена верно.",
            adjustedScore: "62",
        });
        expect(validateDisputeVerdict(rejectionWithScore).canSubmit).toBe(true);
        expect(buildResolveDisputeRequest(NOTE_ID, rejectionWithScore)?.adjustedScore).toBeNull();
    });
});
