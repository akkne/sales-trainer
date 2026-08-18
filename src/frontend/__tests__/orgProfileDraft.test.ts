import { beforeEach, describe, expect, it } from "vitest";
import {
    groupDraftProposals,
    isDraftWithoutEffect,
    toggleAcceptedField,
} from "@/features/org-profile/utils/draft-preview";
import {
    clearHandedOverDraft,
    readHandedOverDraft,
} from "@/features/org-profile/utils/draft-handoff";
import { PROFILE_DRAFT_HANDOFF_STORAGE_KEY } from "@/features/org-profile/constants/profile-fields";
import type { OrganizationProfileFieldProposal } from "@/features/org-profile/types/organization-profile";

const proposal = (
    field: string,
    decision: string,
    overrides: Partial<OrganizationProfileFieldProposal> = {}
): OrganizationProfileFieldProposal => ({
    field,
    decision,
    currentValue: null,
    suggestedValue: null,
    addedItemCount: 0,
    ...overrides,
});

describe("organization profile draft preview — what the screen renders", () => {
    it("drops unchanged proposals entirely: a list of things that will not happen is noise", () => {
        const groups = groupDraftProposals([
            proposal("product", "unchanged"),
            proposal("icp", "fill", { suggestedValue: "Розничные сети" }),
        ]);

        expect(groups.filled.map((entry) => entry.field)).toEqual(["icp"]);
        expect(groups.extended).toEqual([]);
        expect(groups.conflicting).toEqual([]);
    });

    it("orders every section by the interview's ordering, not by the server's array order", () => {
        const groups = groupDraftProposals([
            proposal("tone", "conflict", { currentValue: "на равных", suggestedValue: "строго" }),
            proposal("product", "conflict", { currentValue: "A", suggestedValue: "B" }),
        ]);

        expect(groups.conflicting.map((entry) => entry.field)).toEqual(["product", "tone"]);
    });

    it("treats an extend of zero items as nothing to show", () => {
        const groups = groupDraftProposals([proposal("glossary", "extend", { addedItemCount: 0 })]);
        expect(groups.extended).toEqual([]);
    });

    it("recognises a draft that would change nothing at all", () => {
        expect(
            isDraftWithoutEffect([proposal("product", "unchanged"), proposal("icp", "unchanged")])
        ).toBe(true);

        expect(isDraftWithoutEffect([proposal("icp", "fill", { suggestedValue: "x" })])).toBe(
            false
        );
    });
});

/**
 * Conflicts start unticked and `banned_claims` can never be ticked at all: the merge is add-only on
 * the server precisely so that no client can delete a compliance rule by naming it here, and a
 * screen that offered the tick would be promising something that silently would not happen.
 */
describe("organization profile draft preview — accepting a conflict", () => {
    it("starts from nothing accepted", () => {
        expect(toggleAcceptedField([], "product")).toEqual(["product"]);
    });

    it("unticks what was ticked", () => {
        expect(toggleAcceptedField(["product", "tone"], "product")).toEqual(["tone"]);
    });

    it("refuses to accept a field the server would ignore", () => {
        expect(toggleAcceptedField([], "banned_claims")).toEqual([]);
        expect(toggleAcceptedField([], "objections")).toEqual([]);
        expect(toggleAcceptedField([], "glossary")).toEqual([]);
    });

    it("accepts exactly the four overwritable fields", () => {
        expect(toggleAcceptedField([], "product")).toEqual(["product"]);
        expect(toggleAcceptedField([], "icp")).toEqual(["icp"]);
        expect(toggleAcceptedField([], "tone")).toEqual(["tone"]);
        expect(toggleAcceptedField([], "script_stages")).toEqual(["script_stages"]);
    });
});

describe("organization profile draft handoff", () => {
    beforeEach(() => {
        window.sessionStorage.clear();
    });

    it("reads nothing when the checkpoint left nothing", () => {
        expect(readHandedOverDraft()).toBeNull();
    });

    it("reads the structure the checkpoint stored", () => {
        window.sessionStorage.setItem(
            PROFILE_DRAFT_HANDOFF_STORAGE_KEY,
            JSON.stringify({ product: "СРМ", bannedClaims: ["гарантируем доход"] })
        );

        expect(readHandedOverDraft()).toEqual({
            product: "СРМ",
            bannedClaims: ["гарантируем доход"],
        });
    });

    it("survives a corrupted slot rather than breaking the screen", () => {
        window.sessionStorage.setItem(PROFILE_DRAFT_HANDOFF_STORAGE_KEY, "{not json");
        expect(readHandedOverDraft()).toBeNull();
    });

    it("clears the slot so a reload does not re-open the preview", () => {
        window.sessionStorage.setItem(PROFILE_DRAFT_HANDOFF_STORAGE_KEY, JSON.stringify({}));
        clearHandedOverDraft();
        expect(window.sessionStorage.getItem(PROFILE_DRAFT_HANDOFF_STORAGE_KEY)).toBeNull();
    });
});
