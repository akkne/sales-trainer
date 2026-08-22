import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useOnboardingSkillSelectionStore } from "@/shared/stores/onboarding-skill-selection-store";
import { submitOnboarding } from "@/features/auth/hooks/use-onboarding";

const STORAGE_KEY = "onboarding.skillSelectionUnsaved";

/**
 * Q-6 (`docs/NIGHT_AUDIT_QUESTIONS.md`). Onboarding's `PUT /skills/enrolled` stays best-effort, so
 * this flag is the entire mechanism that keeps its failure from being invisible: it is written by
 * the onboarding mutation, read by the `/tree` banner, and cleared by any successful enrollment.
 * The `localStorage` half is the part that matters and the part a refactor can silently drop — an
 * in-memory-only flag would lose the fact on the first reload of `/tree`, which is exactly the
 * "user never finds out" outcome W-13 was filed about.
 */
describe("onboarding skill-selection flag", () => {
    beforeEach(() => {
        localStorage.clear();
        useOnboardingSkillSelectionStore.setState({ isSkillSelectionUnsaved: false });
    });

    it("starts clear, so a normal onboarding shows no banner", () => {
        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(false);
        expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
    });

    it("survives a reload of /tree once onboarding could not save the selection", () => {
        useOnboardingSkillSelectionStore.getState().markSkillSelectionUnsaved();

        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(true);
        expect(localStorage.getItem(STORAGE_KEY)).toBe("true");

        // What a fresh page load does: the store starts at its default and hydrates in an effect.
        useOnboardingSkillSelectionStore.setState({ isSkillSelectionUnsaved: false });
        useOnboardingSkillSelectionStore.getState().hydrateFromStorage();

        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(true);
    });

    it("stays cleared across a reload once the selection is saved or the banner dismissed", () => {
        useOnboardingSkillSelectionStore.getState().markSkillSelectionUnsaved();
        useOnboardingSkillSelectionStore.getState().clearSkillSelectionUnsaved();

        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(false);
        // Removed, not left as "false": nothing should keep reading a key that means nothing.
        expect(localStorage.getItem(STORAGE_KEY)).toBeNull();

        useOnboardingSkillSelectionStore.getState().hydrateFromStorage();
        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(false);
    });

    it("reads any value other than the exact marker as clear", () => {
        localStorage.setItem(STORAGE_KEY, "yes");
        useOnboardingSkillSelectionStore.getState().hydrateFromStorage();

        expect(useOnboardingSkillSelectionStore.getState().isSkillSelectionUnsaved).toBe(false);
    });
});

const PAYLOAD = {
    salesType: "b2b",
    experienceLevel: "junior",
    selectedSkillSlugs: ["cold-calling", "objection-handling"],
};

function emptyResponse(status: number) {
    return {
        ok: status >= 200 && status < 300,
        status,
        json: async () => ({}),
        text: async () => "",
    } as unknown as Response;
}

/**
 * The asymmetry between the two writes is the whole decision, so it is what is asserted: the first
 * one may sink onboarding, the second one may not — but the second one's failure has to come back
 * as a fact the caller can act on, not as a silence.
 */
describe("submitOnboarding", () => {
    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it("reports the skill selection saved when both writes succeed", async () => {
        vi.stubGlobal("fetch", vi.fn(async () => emptyResponse(204)));

        await expect(submitOnboarding(PAYLOAD)).resolves.toEqual({
            didPersistSkillSelection: true,
        });
    });

    it("still completes onboarding when only the skill enrollment fails, and says so", async () => {
        const fetchMock = vi.fn(async (input: unknown) =>
            String(input).includes("/skills/enrolled") ? emptyResponse(500) : emptyResponse(204)
        );
        vi.stubGlobal("fetch", fetchMock);

        // Resolves rather than throws: the user is not trapped on the onboarding screen.
        await expect(submitOnboarding(PAYLOAD)).resolves.toEqual({
            didPersistSkillSelection: false,
        });
    });

    it("fails the whole thing when the onboarding write itself fails", async () => {
        const fetchMock = vi.fn(async (input: unknown) =>
            String(input).includes("/onboarding") ? emptyResponse(500) : emptyResponse(204)
        );
        vi.stubGlobal("fetch", fetchMock);

        await expect(submitOnboarding(PAYLOAD)).rejects.toThrow();
        // The enrollment write is never attempted: there is no onboarding to enroll against.
        expect(
            fetchMock.mock.calls.some(([input]) => String(input).includes("/skills/enrolled"))
        ).toBe(false);
    });
});
