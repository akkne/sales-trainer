import { describe, expect, it } from "vitest";
import {
    findRemovedBannedClaims,
    moveListItem,
    toProfileFormState,
    toUpdateProfileRequest,
    validateProfileForm,
    type ProfileFormState,
} from "@/features/org-profile/utils/profile-form";
import type { OrganizationProfile } from "@/features/org-profile/types/organization-profile";

const storedProfile: OrganizationProfile = {
    product: "Облачный учёт складских остатков",
    icp: "Розничные сети 50–300 точек",
    objections: [{ text: "дорого", frequency: "часто", bestResponse: "стоимость простоя выше" }],
    scriptStages: ["Контакт", "Потребность", "Закрытие"],
    tone: "на равных",
    glossary: { сделка: "проект" },
    bannedClaims: ["гарантируем рост выручки", "окупится за месяц"],
    createdAt: "2026-05-20T10:00:00Z",
    updatedAt: "2026-08-18T10:00:00Z",
};

const emptyFormState = (overrides: Partial<ProfileFormState> = {}): ProfileFormState => ({
    product: "",
    icp: "",
    tone: "",
    objections: [],
    scriptStages: [],
    glossaryEntries: [],
    bannedClaims: [],
    ...overrides,
});

describe("organization profile full form — loading and saving", () => {
    it("turns a missing profile into a form with no nulls in it", () => {
        expect(toProfileFormState(null)).toEqual(emptyFormState());
    });

    it("round-trips a stored profile without inventing or losing anything", () => {
        expect(toUpdateProfileRequest(toProfileFormState(storedProfile))).toEqual({
            product: storedProfile.product,
            icp: storedProfile.icp,
            tone: storedProfile.tone,
            objections: storedProfile.objections,
            scriptStages: storedProfile.scriptStages,
            glossary: storedProfile.glossary,
            bannedClaims: storedProfile.bannedClaims,
        });
    });

    it("clears a field that was emptied — the whole-row form is the only place that can", () => {
        const request = toUpdateProfileRequest(
            toProfileFormState({ ...storedProfile, product: "Что-то" })
        );
        expect(request.product).toBe("Что-то");

        const cleared = toUpdateProfileRequest(
            emptyFormState({ icp: storedProfile.icp ?? "" })
        );
        expect(cleared.product).toBeNull();
        expect(cleared.bannedClaims).toEqual([]);
    });

    it("drops blank rows rather than storing an objection with no text", () => {
        const request = toUpdateProfileRequest(
            emptyFormState({
                objections: [
                    { text: " дорого ", frequency: " ", bestResponse: "" },
                    { text: "  ", frequency: "", bestResponse: "" },
                ],
                scriptStages: ["Контакт", "   "],
                glossaryEntries: [
                    { term: " сделка ", definition: " проект " },
                    { term: "лид", definition: " " },
                ],
                bannedClaims: [" гарантируем доход ", ""],
            })
        );

        expect(request.objections).toEqual([
            { text: "дорого", frequency: null, bestResponse: null },
        ]);
        expect(request.scriptStages).toEqual(["Контакт"]);
        expect(request.glossary).toEqual({ сделка: "проект" });
        expect(request.bannedClaims).toEqual(["гарантируем доход"]);
    });
});

describe("organization profile full form — validation", () => {
    it("accepts an entirely empty profile: emptiness is legal here", () => {
        expect(validateProfileForm(emptyFormState())).toEqual({});
    });

    it("refuses a glossary term written twice, because only one definition would survive", () => {
        const errors = validateProfileForm(
            emptyFormState({
                glossaryEntries: [
                    { term: "Сделка", definition: "проект" },
                    { term: "сделка", definition: "контракт" },
                ],
            })
        );

        expect(errors.glossary).toMatch(/дважды/);
    });

    it("refuses a term with no meaning", () => {
        expect(
            validateProfileForm(
                emptyFormState({ glossaryEntries: [{ term: "лид", definition: " " }] })
            ).glossary
        ).toMatch(/значение/);
    });

    it("refuses an answer attached to no objection", () => {
        expect(
            validateProfileForm(
                emptyFormState({
                    objections: [{ text: "  ", frequency: "", bestResponse: "ответ есть" }],
                })
            ).objections
        ).toBeTruthy();
    });

    it("refuses the same banned claim twice", () => {
        expect(
            validateProfileForm(
                emptyFormState({ bannedClaims: ["Гарантируем доход", "гарантируем доход"] })
            ).banned_claims
        ).toBeTruthy();
    });
});

/**
 * `banned_claims` is what keeps the AI persona from voicing a promise the customer's lawyer forbade
 * and the grader from rewarding a rep for voicing it. Every other path into the field is add-only;
 * this form is the one place a claim can leave, so the screen has to know exactly which ones would.
 */
describe("organization profile full form — removing a banned claim", () => {
    it("names every claim a save would stop forbidding", () => {
        expect(
            findRemovedBannedClaims(storedProfile.bannedClaims, ["гарантируем рост выручки"])
        ).toEqual(["окупится за месяц"]);
    });

    it("does not count a re-typed claim as removed, whitespace and case aside", () => {
        expect(
            findRemovedBannedClaims(
                ["Гарантируем рост выручки"],
                ["  гарантируем рост выручки  "]
            )
        ).toEqual([]);
    });

    it("counts a claim blanked out in place as a removal", () => {
        expect(findRemovedBannedClaims(["окупится за месяц"], ["   "])).toEqual([
            "окупится за месяц",
        ]);
    });

    it("reports nothing removed when the list only grows", () => {
        expect(
            findRemovedBannedClaims(["окупится за месяц"], ["окупится за месяц", "вернём деньги"])
        ).toEqual([]);
    });
});

describe("script stage reordering", () => {
    it("moves one stage and leaves the rest in order", () => {
        expect(moveListItem(["A", "B", "C"], 2, 0)).toEqual(["C", "A", "B"]);
    });

    it("leaves the list alone when the target is out of range", () => {
        expect(moveListItem(["A", "B"], 0, 5)).toEqual(["A", "B"]);
        expect(moveListItem(["A", "B"], 1, 1)).toEqual(["A", "B"]);
    });
});
