import {
    STRUCTURE_MAXIMUM_BANNED_CLAIM_COUNT,
    STRUCTURE_MAXIMUM_GLOSSARY_TERM_COUNT,
    STRUCTURE_MAXIMUM_OBJECTION_COUNT,
    STRUCTURE_MAXIMUM_SCRIPT_STAGE_COUNT,
    STRUCTURE_VALUE_MAXIMUM_LENGTH,
} from "@/features/org-content-generation/constants/generation-dictionary";
import type { ContentStructure } from "@/features/org-content-generation/types/content-generation";

/**
 * The checkpoint's editable document.
 *
 * It is not `ContentStructure` itself for one reason: a glossary on the wire is a `Record`, and a
 * record cannot hold the half-typed row «» → «доставка» that exists for the second and a half
 * between adding a term and naming it. So the editor keeps ordered pairs and the record is rebuilt
 * on the way out. Scalars are `""` rather than `null` here for the same reason — a controlled
 * input has no null.
 */
export interface GlossaryEntryDraft {
    term: string;
    definition: string;
}

export interface ObjectionDraft {
    text: string;
    bestResponse: string;
}

export interface ContentStructureDraft {
    product: string;
    icp: string;
    tone: string;
    objections: ObjectionDraft[];
    scriptStages: string[];
    glossaryEntries: GlossaryEntryDraft[];
    bannedClaims: string[];
}

export const EMPTY_STRUCTURE_DRAFT: ContentStructureDraft = {
    product: "",
    icp: "",
    tone: "",
    objections: [],
    scriptStages: [],
    glossaryEntries: [],
    bannedClaims: [],
};

export function toStructureDraft(structure: ContentStructure | null): ContentStructureDraft {
    if (!structure) return EMPTY_STRUCTURE_DRAFT;

    return {
        product: structure.product ?? "",
        icp: structure.icp ?? "",
        tone: structure.tone ?? "",
        objections: structure.objections.map((objection) => ({
            text: objection.text,
            bestResponse: objection.bestResponse ?? "",
        })),
        scriptStages: [...structure.scriptStages],
        glossaryEntries: Object.entries(structure.glossary).map(([term, definition]) => ({
            term,
            definition,
        })),
        bannedClaims: [...structure.bannedClaims],
    };
}

function trimToNull(value: string): string | null {
    const trimmedValue = value.trim();
    return trimmedValue.length > 0 ? trimmedValue : null;
}

/**
 * The draft as `PUT …/structure` wants it.
 *
 * Blank rows are dropped rather than sent: a row somebody added and never filled in is an
 * unfinished gesture, and sending it would put an empty objection into the prompt. **A gap stays a
 * gap** — an empty product goes as `null`, never as `""`, because the generation prompt treats the
 * two differently and the profile promotion would copy the empty string across.
 *
 * A duplicate glossary term keeps the last definition typed, matching the server's own
 * case-insensitive dictionary rather than fighting it.
 */
export function toStructurePayload(draft: ContentStructureDraft): ContentStructure {
    const glossary: Record<string, string> = {};
    for (const entry of draft.glossaryEntries) {
        const term = entry.term.trim();
        const definition = entry.definition.trim();
        if (term.length === 0 || definition.length === 0) continue;
        glossary[term] = definition;
    }

    return {
        product: trimToNull(draft.product),
        icp: trimToNull(draft.icp),
        tone: trimToNull(draft.tone),
        objections: draft.objections
            .filter((objection) => objection.text.trim().length > 0)
            .map((objection) => ({
                text: objection.text.trim(),
                bestResponse: trimToNull(objection.bestResponse),
            })),
        scriptStages: draft.scriptStages
            .map((stage) => stage.trim())
            .filter((stage) => stage.length > 0),
        glossary,
        bannedClaims: draft.bannedClaims
            .map((claim) => claim.trim())
            .filter((claim) => claim.length > 0),
    };
}

/**
 * Whether the debounced autosave has anything to say. Compared on the payload rather than on the
 * draft so that adding an empty row, or typing and deleting a space, does not issue a `PUT` — the
 * indicator reading «сохранено 14:22» has to mean something was.
 */
export function hasStructurePayloadChanged(
    draft: ContentStructureDraft,
    lastSavedStructure: ContentStructure | null
): boolean {
    return (
        JSON.stringify(toStructurePayload(draft)) !==
        JSON.stringify(toStructurePayload(toStructureDraft(lastSavedStructure)))
    );
}

/** The four caps, in one place, so «7 из 10» and the disabled «+ добавить» cannot disagree. */
export const STRUCTURE_LIST_CAPS = {
    objections: STRUCTURE_MAXIMUM_OBJECTION_COUNT,
    scriptStages: STRUCTURE_MAXIMUM_SCRIPT_STAGE_COUNT,
    glossaryEntries: STRUCTURE_MAXIMUM_GLOSSARY_TERM_COUNT,
    bannedClaims: STRUCTURE_MAXIMUM_BANNED_CLAIM_COUNT,
} as const;

export type StructureListName = keyof typeof STRUCTURE_LIST_CAPS;

export function countStructureListEntries(
    draft: ContentStructureDraft,
    listName: StructureListName
): number {
    return draft[listName].length;
}

export function isStructureListAtCap(
    draft: ContentStructureDraft,
    listName: StructureListName
): boolean {
    return countStructureListEntries(draft, listName) >= STRUCTURE_LIST_CAPS[listName];
}

/**
 * «Возражения (7 из 10)». The cap is shown even at zero — the design's own rule is that a gap is
 * shown as a gap, so «Запрещённые обещания (0 из 20) — пусто» is the wanted reading, not a hidden
 * section.
 */
export function describeStructureListCount(
    draft: ContentStructureDraft,
    listName: StructureListName
): string {
    return `${countStructureListEntries(draft, listName)} из ${STRUCTURE_LIST_CAPS[listName]}`;
}

/** Enforced on the input as `maxLength`, mirrored here so a paste cannot exceed the server's cap. */
export function clampStructureValue(value: string): string {
    return value.length > STRUCTURE_VALUE_MAXIMUM_LENGTH
        ? value.slice(0, STRUCTURE_VALUE_MAXIMUM_LENGTH)
        : value;
}

/** Saved at 14:22, not «недавно» — the reviewer wants to know whether their last edit made it. */
export function formatSavedAtTime(savedAt: Date): string {
    const hours = String(savedAt.getHours()).padStart(2, "0");
    const minutes = String(savedAt.getMinutes()).padStart(2, "0");
    return `${hours}:${minutes}`;
}
