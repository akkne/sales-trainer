import {
    MINIMUM_OBJECTION_COUNT,
    MINIMUM_SCRIPT_STAGE_COUNT,
    OPTIONAL_ANSWER_FIELDS,
    ORGANIZATION_PROFILE_FIELDS,
    PROFILE_GAP_PRIORITIES,
} from "../constants/profile-fields";
import type {
    OrganizationProfileGap,
    OrganizationProfileGaps,
    PatchOrganizationProfileRequest,
} from "../types/organization-profile";

export interface ObjectionDraft {
    text: string;
    bestResponse: string;
}

export interface GlossaryEntryDraft {
    term: string;
    definition: string;
}

/**
 * What kind of editor a gap code needs. Four shapes cover seven fields, and the mapping is closed:
 * a code this build does not know gets no editor rather than a guessed one.
 */
export type ProfileAnswerEditorKind = "text" | "stringList" | "objections" | "glossary";

export type ProfileAnswerDraft =
    | { kind: "text"; text: string }
    | { kind: "stringList"; items: string[] }
    | { kind: "objections"; objections: ObjectionDraft[] }
    | { kind: "glossary"; entries: GlossaryEntryDraft[] };

const EDITOR_KIND_BY_FIELD: Record<string, ProfileAnswerEditorKind> = {
    [ORGANIZATION_PROFILE_FIELDS.product]: "text",
    [ORGANIZATION_PROFILE_FIELDS.icp]: "text",
    [ORGANIZATION_PROFILE_FIELDS.tone]: "text",
    [ORGANIZATION_PROFILE_FIELDS.scriptStages]: "stringList",
    [ORGANIZATION_PROFILE_FIELDS.bannedClaims]: "stringList",
    [ORGANIZATION_PROFILE_FIELDS.objections]: "objections",
    [ORGANIZATION_PROFILE_FIELDS.glossary]: "glossary",
};

export function answerEditorKindFor(fieldCode: string): ProfileAnswerEditorKind | null {
    return EDITOR_KIND_BY_FIELD[fieldCode] ?? null;
}

export function createEmptyAnswerDraft(fieldCode: string): ProfileAnswerDraft | null {
    switch (answerEditorKindFor(fieldCode)) {
        case "text":
            return { kind: "text", text: "" };
        case "stringList":
            return { kind: "stringList", items: [""] };
        case "objections":
            return {
                kind: "objections",
                objections: Array.from({ length: MINIMUM_OBJECTION_COUNT }, () => ({
                    text: "",
                    bestResponse: "",
                })),
            };
        case "glossary":
            return { kind: "glossary", entries: [{ term: "", definition: "" }] };
        default:
            return null;
    }
}

/**
 * Which questions this round actually shows.
 *
 * Two rules on top of what the server sent. `banned_claims` and the glossary are hidden while any
 * blocking gap is open — they are the two whose honest answer may be «таких нет», and asking them
 * before the product is described spends the customer's five minutes on the fields that change the
 * least. And a question somebody skipped in this sitting does not come back on the next refetch,
 * because the schema has no «answered: nothing» marker to record the skip in.
 */
export function selectVisibleQuestions(
    gaps: OrganizationProfileGaps | undefined,
    skippedFieldCodes: readonly string[] = []
): OrganizationProfileGap[] {
    if (!gaps) return [];

    const hasOpenBlockingGap = gaps.blockingGapCount > 0;

    return gaps.questions.filter((question) => {
        if (skippedFieldCodes.includes(question.code)) return false;
        if (answerEditorKindFor(question.code) === null) return false;
        if (hasOpenBlockingGap && OPTIONAL_ANSWER_FIELDS.includes(question.code)) return false;
        return true;
    });
}

/**
 * «Осталось ещё N». Counted against what is on screen rather than against the server's capped list,
 * so hiding a question does not make the total lie. Never negative: the two counts come from one
 * response, but a skipped question is subtracted from a total that never knew about the skip.
 */
export function countRemainingQuestions(
    gaps: OrganizationProfileGaps | undefined,
    visibleQuestionCount: number
): number {
    if (!gaps) return 0;
    return Math.max(0, gaps.totalGapCount - visibleQuestionCount);
}

export function isBlockingGap(gap: OrganizationProfileGap): boolean {
    return gap.priority === PROFILE_GAP_PRIORITIES.blocking;
}

const cleanedTextLines = (values: readonly string[]): string[] =>
    values.map((value) => value.trim()).filter((value) => value.length > 0);

/**
 * Why an answer cannot be sent yet, in Russian, or `null` when it can. The two count thresholds
 * mirror `OrganizationProfileGapCodes` — sending two objections would be accepted by the server and
 * come straight back as the same gap, which reads as a save that did nothing.
 */
export function validateAnswerDraft(fieldCode: string, draft: ProfileAnswerDraft): string | null {
    switch (draft.kind) {
        case "text":
            return draft.text.trim().length === 0 ? "Впишите ответ, поле пустое." : null;

        case "stringList": {
            const items = cleanedTextLines(draft.items);
            if (items.length === 0) return "Добавьте хотя бы одну строку.";
            if (
                fieldCode === ORGANIZATION_PROFILE_FIELDS.scriptStages &&
                items.length < MINIMUM_SCRIPT_STAGE_COUNT
            ) {
                return `Этапов должно быть минимум ${MINIMUM_SCRIPT_STAGE_COUNT} — начало, середина и договорённость.`;
            }
            return null;
        }

        case "objections": {
            const objections = draft.objections.filter(
                (objection) => objection.text.trim().length > 0
            );
            if (objections.length < MINIMUM_OBJECTION_COUNT) {
                return `Возражений должно быть минимум ${MINIMUM_OBJECTION_COUNT} — с двумя собеседник узнаётся как скрипт со второго диалога.`;
            }
            return null;
        }

        case "glossary": {
            const entries = draft.entries.filter(
                (entry) => entry.term.trim().length > 0 || entry.definition.trim().length > 0
            );
            if (entries.length === 0) return "Добавьте хотя бы одну пару «термин — значение».";
            if (entries.some((entry) => entry.term.trim().length === 0)) {
                return "У каждой пары должен быть термин.";
            }
            if (entries.some((entry) => entry.definition.trim().length === 0)) {
                return "У каждого термина должно быть значение.";
            }
            return null;
        }
    }
}

/**
 * One answer becomes a patch naming exactly one field. Everything else is omitted, and an omitted
 * field keeps whatever a colleague saved while this question was on screen — that is the whole
 * reason the interview patches instead of reading, splicing and putting all seven back.
 */
export function buildPatchForAnswer(
    fieldCode: string,
    draft: ProfileAnswerDraft
): PatchOrganizationProfileRequest {
    switch (draft.kind) {
        case "text": {
            const text = draft.text.trim();
            if (fieldCode === ORGANIZATION_PROFILE_FIELDS.product) return { product: text };
            if (fieldCode === ORGANIZATION_PROFILE_FIELDS.icp) return { icp: text };
            return { tone: text };
        }

        case "stringList": {
            const items = cleanedTextLines(draft.items);
            return fieldCode === ORGANIZATION_PROFILE_FIELDS.scriptStages
                ? { scriptStages: items }
                : { bannedClaims: items };
        }

        case "objections":
            return {
                objections: draft.objections
                    .filter((objection) => objection.text.trim().length > 0)
                    .map((objection) => ({
                        text: objection.text.trim(),
                        frequency: null,
                        bestResponse:
                            objection.bestResponse.trim().length > 0
                                ? objection.bestResponse.trim()
                                : null,
                    })),
            };

        case "glossary":
            return {
                glossary: Object.fromEntries(
                    draft.entries
                        .filter(
                            (entry) =>
                                entry.term.trim().length > 0 && entry.definition.trim().length > 0
                        )
                        .map((entry) => [entry.term.trim(), entry.definition.trim()])
                ),
            };
    }
}
