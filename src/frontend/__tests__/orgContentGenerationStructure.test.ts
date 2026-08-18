import { describe, expect, it } from "vitest";

import {
    STRUCTURE_MAXIMUM_BANNED_CLAIM_COUNT,
    STRUCTURE_MAXIMUM_GLOSSARY_TERM_COUNT,
    STRUCTURE_MAXIMUM_OBJECTION_COUNT,
    STRUCTURE_MAXIMUM_SCRIPT_STAGE_COUNT,
    STRUCTURE_VALUE_MAXIMUM_LENGTH,
} from "@/features/org-content-generation/constants/generation-dictionary";
import {
    EMPTY_STRUCTURE_DRAFT,
    clampStructureValue,
    describeStructureListCount,
    formatSavedAtTime,
    hasStructurePayloadChanged,
    isStructureListAtCap,
    toStructureDraft,
    toStructurePayload,
    type ContentStructureDraft,
} from "@/features/org-content-generation/utils/structure-draft";
import {
    describeAdaptationsQueue,
    describeExerciseCount,
    describeOverridesQueue,
    describeOwnLessonsQueue,
    formatRunTimestamp,
    pluralizeRussianCount,
} from "@/features/org-content-generation/utils/queue-copy";
import type { ContentQueueCounts } from "@/features/org-content-generation/hooks/use-content-hub-counters";
import type { ContentStructure } from "@/features/org-content-generation/types/content-generation";

/**
 * The checkpoint document (O11 layout в) and the hub's counters (O9).
 *
 * The rule that gets its own test here is **«пробел остаётся пробелом»**: an empty field must reach
 * the server as `null` and never as `""`, because the generation prompt reads the two differently
 * and 40.29's profile promotion would copy an empty string over a value a human entered.
 */

const FULL_STRUCTURE: ContentStructure = {
    product: "Облачная система учёта складских остатков",
    icp: "Розничные сети 50–300 точек",
    tone: "консультативный",
    objections: [
        { text: "Дорого", bestResponse: "Считаем не цену, а стоимость простоя" },
        { text: "Уже есть поставщик", bestResponse: null },
    ],
    scriptStages: ["Приветствие", "Выявление потребности"],
    glossary: { "СДЭК": "служба доставки" },
    bannedClaims: ["гарантированная доходность"],
};

describe("toStructureDraft", () => {
    it("turns nulls into the empty strings a controlled input needs", () => {
        const draft = toStructureDraft({ ...FULL_STRUCTURE, product: null, tone: null });

        expect(draft.product).toBe("");
        expect(draft.tone).toBe("");
        expect(draft.objections[1].bestResponse).toBe("");
    });

    it("turns the glossary record into ordered pairs the editor can hold half-typed", () => {
        expect(toStructureDraft(FULL_STRUCTURE).glossaryEntries).toEqual([
            { term: "СДЭК", definition: "служба доставки" },
        ]);
    });

    it("makes an empty draft out of a run that has no structure yet", () => {
        expect(toStructureDraft(null)).toEqual(EMPTY_STRUCTURE_DRAFT);
    });
});

describe("toStructurePayload", () => {
    it("sends a blank scalar as null, never as an empty string — a gap stays a gap", () => {
        const payload = toStructurePayload({
            ...EMPTY_STRUCTURE_DRAFT,
            product: "   ",
            icp: "",
            tone: "консультативный",
        });

        expect(payload.product).toBeNull();
        expect(payload.icp).toBeNull();
        expect(payload.tone).toBe("консультативный");
    });

    it("drops a row somebody added and never filled in", () => {
        const payload = toStructurePayload({
            ...EMPTY_STRUCTURE_DRAFT,
            objections: [
                { text: "Дорого", bestResponse: "" },
                { text: "   ", bestResponse: "не важно" },
            ],
            scriptStages: ["Приветствие", "  "],
            bannedClaims: ["", "гарантия"],
        });

        expect(payload.objections).toEqual([{ text: "Дорого", bestResponse: null }]);
        expect(payload.scriptStages).toEqual(["Приветствие"]);
        expect(payload.bannedClaims).toEqual(["гарантия"]);
    });

    it("drops a glossary entry that has only half of its pair", () => {
        const payload = toStructurePayload({
            ...EMPTY_STRUCTURE_DRAFT,
            glossaryEntries: [
                { term: "СДЭК", definition: "служба доставки" },
                { term: "", definition: "осиротевшее значение" },
                { term: "ЛПР", definition: "  " },
            ],
        });

        expect(payload.glossary).toEqual({ "СДЭК": "служба доставки" });
    });

    it("keeps the last definition typed for a duplicated term, matching the server's dictionary", () => {
        const payload = toStructurePayload({
            ...EMPTY_STRUCTURE_DRAFT,
            glossaryEntries: [
                { term: "СДЭК", definition: "первое" },
                { term: "СДЭК", definition: "второе" },
            ],
        });

        expect(payload.glossary).toEqual({ "СДЭК": "второе" });
    });

    it("round-trips a full structure unchanged", () => {
        expect(toStructurePayload(toStructureDraft(FULL_STRUCTURE))).toEqual(FULL_STRUCTURE);
    });
});

describe("hasStructurePayloadChanged", () => {
    it("is false when nothing meaningful changed, so autosave issues no PUT", () => {
        expect(hasStructurePayloadChanged(toStructureDraft(FULL_STRUCTURE), FULL_STRUCTURE)).toBe(
            false
        );
    });

    it("is false for an added-but-empty row: the indicator must only claim real saves", () => {
        const draft: ContentStructureDraft = {
            ...toStructureDraft(FULL_STRUCTURE),
            scriptStages: [...FULL_STRUCTURE.scriptStages, ""],
        };

        expect(hasStructurePayloadChanged(draft, FULL_STRUCTURE)).toBe(false);
    });

    it("is false for trailing whitespace alone", () => {
        const draft: ContentStructureDraft = {
            ...toStructureDraft(FULL_STRUCTURE),
            product: `${FULL_STRUCTURE.product}   `,
        };

        expect(hasStructurePayloadChanged(draft, FULL_STRUCTURE)).toBe(false);
    });

    it("is true once a value actually differs", () => {
        const draft: ContentStructureDraft = {
            ...toStructureDraft(FULL_STRUCTURE),
            objections: [{ text: "Дорого", bestResponse: "новый ответ" }],
        };

        expect(hasStructurePayloadChanged(draft, FULL_STRUCTURE)).toBe(true);
    });

    it("is true when the server holds nothing and the reviewer typed their own structure", () => {
        const draft: ContentStructureDraft = {
            ...EMPTY_STRUCTURE_DRAFT,
            objections: [{ text: "Дорого", bestResponse: "" }],
        };

        expect(hasStructurePayloadChanged(draft, null)).toBe(true);
        expect(hasStructurePayloadChanged(EMPTY_STRUCTURE_DRAFT, null)).toBe(false);
    });
});

describe("the caps, which are the server's own", () => {
    it("mirrors ContentStructureDocumentSerializer exactly", () => {
        expect(STRUCTURE_MAXIMUM_OBJECTION_COUNT).toBe(10);
        expect(STRUCTURE_MAXIMUM_SCRIPT_STAGE_COUNT).toBe(12);
        expect(STRUCTURE_MAXIMUM_GLOSSARY_TERM_COUNT).toBe(30);
        expect(STRUCTURE_MAXIMUM_BANNED_CLAIM_COUNT).toBe(20);
        expect(STRUCTURE_VALUE_MAXIMUM_LENGTH).toBe(2000);
    });

    it("reads «7 из 10» off the same numbers the «+ добавить» button is disabled by", () => {
        const draft: ContentStructureDraft = {
            ...EMPTY_STRUCTURE_DRAFT,
            objections: Array.from({ length: 7 }, (_, index) => ({
                text: `возражение ${index}`,
                bestResponse: "",
            })),
        };

        expect(describeStructureListCount(draft, "objections")).toBe("7 из 10");
        expect(isStructureListAtCap(draft, "objections")).toBe(false);
    });

    it("disables «+ добавить» at the cap rather than letting the server silently truncate", () => {
        const draft: ContentStructureDraft = {
            ...EMPTY_STRUCTURE_DRAFT,
            scriptStages: Array.from({ length: 12 }, (_, index) => `этап ${index}`),
        };

        expect(isStructureListAtCap(draft, "scriptStages")).toBe(true);
        expect(describeStructureListCount(draft, "scriptStages")).toBe("12 из 12");
    });

    it("shows an empty list as «0 из 20» rather than hiding the section", () => {
        expect(describeStructureListCount(EMPTY_STRUCTURE_DRAFT, "bannedClaims")).toBe("0 из 20");
        expect(isStructureListAtCap(EMPTY_STRUCTURE_DRAFT, "bannedClaims")).toBe(false);
    });

    it("clamps a pasted value at 2000 characters", () => {
        expect(clampStructureValue("а".repeat(2500))).toHaveLength(2000);
        expect(clampStructureValue("коротко")).toBe("коротко");
    });
});

describe("formatSavedAtTime", () => {
    it("reads as a wall clock with a leading zero, not as «недавно»", () => {
        expect(formatSavedAtTime(new Date(2026, 7, 18, 14, 22))).toBe("14:22");
        expect(formatSavedAtTime(new Date(2026, 7, 18, 9, 5))).toBe("09:05");
    });
});

describe("Russian agreement in the hub's counters", () => {
    it("gets the 11–14 exception right", () => {
        expect(pluralizeRussianCount(1, "прогон", "прогона", "прогонов")).toBe("прогон");
        expect(pluralizeRussianCount(2, "прогон", "прогона", "прогонов")).toBe("прогона");
        expect(pluralizeRussianCount(5, "прогон", "прогона", "прогонов")).toBe("прогонов");
        expect(pluralizeRussianCount(11, "прогон", "прогона", "прогонов")).toBe("прогонов");
        expect(pluralizeRussianCount(21, "прогон", "прогона", "прогонов")).toBe("прогон");
        expect(pluralizeRussianCount(0, "прогон", "прогона", "прогонов")).toBe("прогонов");
    });

    it("agrees «упражнение / упражнения / упражнений» on a finished run", () => {
        expect(describeExerciseCount(1)).toBe("1 упражнение");
        expect(describeExerciseCount(4)).toBe("4 упражнения");
        expect(describeExerciseCount(12)).toBe("12 упражнений");
    });
});

describe("the O9 queue cards", () => {
    const EMPTY_COUNTS: ContentQueueCounts = {
        awaitingReviewRunCount: 0,
        insufficientRunCount: 0,
        completedRunCount: 0,
        totalRunCount: 0,
        awaitingReviewProposalCount: 0,
        staleOverrideCount: 0,
        totalOverrideCount: 0,
    };

    it("explains the section instead of printing zeros when a queue is empty", () => {
        const ownLessons = describeOwnLessonsQueue(EMPTY_COUNTS);

        expect(ownLessons.lines).toEqual([]);
        expect(ownLessons.emptyDescription).toContain("Загрузите материалы внутреннего тренинга");
        expect(ownLessons.emptyDescription).not.toContain("0");

        expect(describeAdaptationsQueue(EMPTY_COUNTS).lines).toEqual([]);
        expect(describeOverridesQueue(EMPTY_COUNTS).lines).toEqual([]);
    });

    it("counts the checkpoint, the refusals and the finished runs separately", () => {
        const lines = describeOwnLessonsQueue({
            ...EMPTY_COUNTS,
            awaitingReviewRunCount: 1,
            insufficientRunCount: 2,
            completedRunCount: 4,
            totalRunCount: 7,
        }).lines;

        expect(lines).toEqual(["1 ждёт проверки", "2 ждут материала", "4 готовых"]);
    });

    it("omits a line whose count is zero rather than writing «0 готовых»", () => {
        expect(
            describeOwnLessonsQueue({ ...EMPTY_COUNTS, awaitingReviewRunCount: 1 }).lines
        ).toEqual(["1 ждёт проверки"]);
    });

    it("counts proposals awaiting an answer, agreeing the verb with the number", () => {
        expect(
            describeAdaptationsQueue({ ...EMPTY_COUNTS, awaitingReviewProposalCount: 9 }).lines
        ).toEqual(["9 предложений ждут вашего ответа"]);
        expect(
            describeAdaptationsQueue({ ...EMPTY_COUNTS, awaitingReviewProposalCount: 1 }).lines
        ).toEqual(["1 предложение ждёт вашего ответа"]);
    });

    it("shows stale overrides above the total, as the design's mock does", () => {
        expect(
            describeOverridesQueue({
                ...EMPTY_COUNTS,
                staleOverrideCount: 3,
                totalOverrideCount: 7,
            }).lines
        ).toEqual(["3 устарели", "7 всего"]);
    });
});

describe("formatRunTimestamp", () => {
    it("prints a day, a month and a wall clock", () => {
        expect(formatRunTimestamp(new Date(2026, 7, 18, 9, 14).toISOString())).toBe(
            "18 августа, 09:14"
        );
    });

    it("prints a dash for an unparseable timestamp rather than «Invalid Date»", () => {
        expect(formatRunTimestamp("не дата")).toBe("—");
    });
});
