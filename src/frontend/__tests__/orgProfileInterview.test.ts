import { describe, expect, it } from "vitest";
import {
    answerEditorKindFor,
    buildPatchForAnswer,
    countRemainingQuestions,
    createEmptyAnswerDraft,
    selectVisibleQuestions,
    validateAnswerDraft,
} from "@/features/org-profile/utils/interview-answers";
import {
    formatOptionalQuestionCount,
    formatQuestionCount,
} from "@/features/org-profile/utils/russian-counts";
import type {
    OrganizationProfileGap,
    OrganizationProfileGaps,
} from "@/features/org-profile/types/organization-profile";

const gap = (
    code: string,
    priority: "blocking" | "important" | "optional"
): OrganizationProfileGap => ({ code, question: `Вопрос про ${code}`, priority });

const gaps = (
    questions: OrganizationProfileGap[],
    overrides: Partial<OrganizationProfileGaps> = {}
): OrganizationProfileGaps => ({
    questions,
    totalGapCount: questions.length,
    blockingGapCount: questions.filter((question) => question.priority === "blocking").length,
    isReadyForParameterization: false,
    ...overrides,
});

/**
 * O8's interview (docs/TENANCY/ADMIN_UI_DESIGN.md). The rules under test are the five 40.29
 * requirements the screen has to satisfy literally, plus the two count thresholds a save would
 * otherwise pass and bounce straight back off the gap inspector.
 */
describe("organization profile interview — which questions are shown", () => {
    it("hides banned claims and the glossary while a blocking gap is still open", () => {
        const visible = selectVisibleQuestions(
            gaps([gap("product", "blocking"), gap("banned_claims", "important"), gap("glossary", "optional")])
        );

        expect(visible.map((question) => question.code)).toEqual(["product"]);
    });

    it("shows them once nothing blocking is left", () => {
        const visible = selectVisibleQuestions(
            gaps([gap("banned_claims", "important"), gap("glossary", "optional")], {
                blockingGapCount: 0,
                isReadyForParameterization: true,
            })
        );

        expect(visible.map((question) => question.code)).toEqual(["banned_claims", "glossary"]);
    });

    it("drops a question skipped in this sitting, because the schema records no such answer", () => {
        const visible = selectVisibleQuestions(
            gaps([gap("banned_claims", "important"), gap("glossary", "optional")], {
                blockingGapCount: 0,
            }),
            ["banned_claims"]
        );

        expect(visible.map((question) => question.code)).toEqual(["glossary"]);
    });

    it("drops a gap code this build has no editor for rather than rendering it blank", () => {
        const visible = selectVisibleQuestions(
            gaps([gap("product", "blocking"), gap("pricing_policy", "blocking")])
        );

        expect(visible.map((question) => question.code)).toEqual(["product"]);
    });

    it("counts what is left against what is on screen and never goes negative", () => {
        expect(countRemainingQuestions(gaps([gap("product", "blocking")], { totalGapCount: 7 }), 3)).toBe(4);
        expect(countRemainingQuestions(gaps([], { totalGapCount: 1 }), 3)).toBe(0);
        expect(countRemainingQuestions(undefined, 0)).toBe(0);
    });
});

describe("organization profile interview — editors per gap code", () => {
    it("maps each of the seven fields to one of the four editors", () => {
        expect(answerEditorKindFor("product")).toBe("text");
        expect(answerEditorKindFor("icp")).toBe("text");
        expect(answerEditorKindFor("tone")).toBe("text");
        expect(answerEditorKindFor("script_stages")).toBe("stringList");
        expect(answerEditorKindFor("banned_claims")).toBe("stringList");
        expect(answerEditorKindFor("objections")).toBe("objections");
        expect(answerEditorKindFor("glossary")).toBe("glossary");
    });

    it("has no editor for a code it does not know", () => {
        expect(answerEditorKindFor("pricing_policy")).toBeNull();
        expect(createEmptyAnswerDraft("pricing_policy")).toBeNull();
    });

    it("opens the objections editor on the three rows the server requires", () => {
        expect(createEmptyAnswerDraft("objections")).toEqual({
            kind: "objections",
            objections: [
                { text: "", bestResponse: "" },
                { text: "", bestResponse: "" },
                { text: "", bestResponse: "" },
            ],
        });
    });
});

describe("organization profile interview — validation", () => {
    it("refuses an empty text answer", () => {
        expect(validateAnswerDraft("product", { kind: "text", text: "   " })).toMatch(/пустое/);
        expect(validateAnswerDraft("product", { kind: "text", text: "СРМ для оптовиков" })).toBeNull();
    });

    it("refuses fewer than three script stages, which the server would count as the same gap", () => {
        expect(
            validateAnswerDraft("script_stages", {
                kind: "stringList",
                items: ["Приветствие", "Закрытие", "  "],
            })
        ).toMatch(/минимум 3/);

        expect(
            validateAnswerDraft("script_stages", {
                kind: "stringList",
                items: ["Приветствие", "Потребность", "Закрытие"],
            })
        ).toBeNull();
    });

    it("accepts a single banned claim — three is an objections rule, not a claims rule", () => {
        expect(
            validateAnswerDraft("banned_claims", {
                kind: "stringList",
                items: ["гарантируем доходность"],
            })
        ).toBeNull();
    });

    it("refuses fewer than three objections", () => {
        expect(
            validateAnswerDraft("objections", {
                kind: "objections",
                objections: [
                    { text: "дорого", bestResponse: "" },
                    { text: "", bestResponse: "не пустой ответ на пустое возражение" },
                    { text: "подумаем", bestResponse: "" },
                ],
            })
        ).toMatch(/минимум 3/);
    });

    it("refuses a glossary pair missing either half", () => {
        expect(
            validateAnswerDraft("glossary", {
                kind: "glossary",
                entries: [{ term: "сделка", definition: "" }],
            })
        ).toMatch(/значение/);

        expect(
            validateAnswerDraft("glossary", {
                kind: "glossary",
                entries: [{ term: "", definition: "проект" }],
            })
        ).toMatch(/термин/);
    });
});

describe("organization profile interview — one answer is one field", () => {
    it("patches exactly the field that was answered and nothing else", () => {
        const patch = buildPatchForAnswer("product", { kind: "text", text: "  СРМ для оптовиков " });

        expect(patch).toEqual({ product: "СРМ для оптовиков" });
        expect(Object.keys(patch)).toHaveLength(1);
    });

    it("sends script stages and banned claims to different fields from the same editor shape", () => {
        expect(
            buildPatchForAnswer("script_stages", { kind: "stringList", items: ["A", " ", "B"] })
        ).toEqual({ scriptStages: ["A", "B"] });

        expect(
            buildPatchForAnswer("banned_claims", {
                kind: "stringList",
                items: [" гарантируем доход ", ""],
            })
        ).toEqual({ bannedClaims: ["гарантируем доход"] });
    });

    it("never invents a frequency the extraction cannot know", () => {
        expect(
            buildPatchForAnswer("objections", {
                kind: "objections",
                objections: [
                    { text: " дорого ", bestResponse: " сравните со стоимостью простоя " },
                    { text: "", bestResponse: "" },
                ],
            })
        ).toEqual({
            objections: [
                {
                    text: "дорого",
                    frequency: null,
                    bestResponse: "сравните со стоимостью простоя",
                },
            ],
        });
    });

    it("drops half-written glossary pairs instead of storing a term with no meaning", () => {
        expect(
            buildPatchForAnswer("glossary", {
                kind: "glossary",
                entries: [
                    { term: " сделка ", definition: " проект " },
                    { term: "лид", definition: "" },
                ],
            })
        ).toEqual({ glossary: { сделка: "проект" } });
    });
});

describe("organization profile interview — Russian counts", () => {
    it("agrees the noun with the number", () => {
        expect(formatQuestionCount(1)).toBe("1 вопрос");
        expect(formatQuestionCount(2)).toBe("2 вопроса");
        expect(formatQuestionCount(5)).toBe("5 вопросов");
        expect(formatQuestionCount(11)).toBe("11 вопросов");
        expect(formatQuestionCount(21)).toBe("21 вопрос");
    });

    it("agrees the adjective too", () => {
        expect(formatOptionalQuestionCount(1)).toBe("1 необязательный вопрос");
        expect(formatOptionalQuestionCount(3)).toBe("3 необязательных вопроса");
    });
});
