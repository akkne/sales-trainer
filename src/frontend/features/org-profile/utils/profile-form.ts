import { ORGANIZATION_PROFILE_FIELDS } from "../constants/profile-fields";
import type {
    OrganizationProfile,
    UpdateOrganizationProfileRequest,
} from "../types/organization-profile";
import type { GlossaryEntryDraft } from "./interview-answers";

export interface ObjectionFormEntry {
    text: string;
    frequency: string;
    bestResponse: string;
}

/**
 * The whole profile as the form holds it while it is being edited: every field a string or an array
 * of strings, nothing nullable. `null` and `""` mean the same thing to a text input, and carrying
 * both through the form is how a field ends up cleared by a render.
 */
export interface ProfileFormState {
    product: string;
    icp: string;
    tone: string;
    objections: ObjectionFormEntry[];
    scriptStages: string[];
    glossaryEntries: GlossaryEntryDraft[];
    bannedClaims: string[];
}

export function toProfileFormState(profile: OrganizationProfile | null): ProfileFormState {
    return {
        product: profile?.product ?? "",
        icp: profile?.icp ?? "",
        tone: profile?.tone ?? "",
        objections: (profile?.objections ?? []).map((objection) => ({
            text: objection.text,
            frequency: objection.frequency ?? "",
            bestResponse: objection.bestResponse ?? "",
        })),
        scriptStages: [...(profile?.scriptStages ?? [])],
        glossaryEntries: Object.entries(profile?.glossary ?? {}).map(([term, definition]) => ({
            term,
            definition,
        })),
        bannedClaims: [...(profile?.bannedClaims ?? [])],
    };
}

const trimmedOrNull = (value: string): string | null => {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
};

/**
 * The form's state as the whole-row replace the server expects. Blank rows are dropped rather than
 * sent: `PUT` stores what it is given, and an empty objection would become an objection with no text
 * that every persona in the organization then has to work around.
 */
export function toUpdateProfileRequest(state: ProfileFormState): UpdateOrganizationProfileRequest {
    return {
        product: trimmedOrNull(state.product),
        icp: trimmedOrNull(state.icp),
        tone: trimmedOrNull(state.tone),
        objections: state.objections
            .filter((objection) => objection.text.trim().length > 0)
            .map((objection) => ({
                text: objection.text.trim(),
                frequency: trimmedOrNull(objection.frequency),
                bestResponse: trimmedOrNull(objection.bestResponse),
            })),
        scriptStages: state.scriptStages
            .map((stage) => stage.trim())
            .filter((stage) => stage.length > 0),
        glossary: Object.fromEntries(
            state.glossaryEntries
                .filter(
                    (entry) => entry.term.trim().length > 0 && entry.definition.trim().length > 0
                )
                .map((entry) => [entry.term.trim(), entry.definition.trim()])
        ),
        bannedClaims: state.bannedClaims
            .map((claim) => claim.trim())
            .filter((claim) => claim.length > 0),
    };
}

/**
 * What the form refuses to save, keyed by field code so each message lands under its own section.
 *
 * Emptiness is not an error here — the whole-row form is the one place a field may legitimately be
 * cleared. What is an error is a row that would be silently dropped or silently overwritten: a
 * glossary term written twice keeps only one definition, and which one it keeps is an implementation
 * detail nobody should have to know.
 */
export function validateProfileForm(state: ProfileFormState): Record<string, string> {
    const errors: Record<string, string> = {};

    const glossaryTerms = state.glossaryEntries
        .map((entry) => entry.term.trim().toLowerCase())
        .filter((term) => term.length > 0);
    if (new Set(glossaryTerms).size !== glossaryTerms.length) {
        errors[ORGANIZATION_PROFILE_FIELDS.glossary] =
            "Один и тот же термин указан дважды — останется только одно значение.";
    } else if (
        state.glossaryEntries.some(
            (entry) => entry.term.trim().length > 0 && entry.definition.trim().length === 0
        )
    ) {
        errors[ORGANIZATION_PROFILE_FIELDS.glossary] = "У каждого термина должно быть значение.";
    }

    if (
        state.objections.some(
            (objection) =>
                objection.text.trim().length === 0 &&
                (objection.bestResponse.trim().length > 0 || objection.frequency.trim().length > 0)
        )
    ) {
        errors[ORGANIZATION_PROFILE_FIELDS.objections] =
            "У возражения есть ответ, но нет самого возражения — такая строка не сохранится.";
    }

    const bannedClaims = state.bannedClaims
        .map((claim) => claim.trim().toLowerCase())
        .filter((claim) => claim.length > 0);
    if (new Set(bannedClaims).size !== bannedClaims.length) {
        errors[ORGANIZATION_PROFILE_FIELDS.bannedClaims] =
            "Одно и то же обещание в списке дважды.";
    }

    return errors;
}

/**
 * Which banned claims this save would stop forbidding.
 *
 * `banned_claims` is the only field in the profile whose loss is not an inconvenience: it is what
 * keeps the AI persona from voicing a promise a customer's lawyer forbade, and what keeps the grader
 * from rewarding a rep for voicing it (docs/CONTENT_PARAMETERIZATION.md §4). Every other path into
 * this field is add-only; this form is the single place a claim can leave, so the removal is named
 * out loud and confirmed before the request goes out.
 */
export function findRemovedBannedClaims(
    storedClaims: readonly string[],
    editedClaims: readonly string[]
): string[] {
    const keptClaims = new Set(
        editedClaims.map((claim) => claim.trim().toLowerCase()).filter((claim) => claim.length > 0)
    );

    return storedClaims.filter((claim) => !keptClaims.has(claim.trim().toLowerCase()));
}

/** Moves one entry of an ordered list; out-of-range targets leave the list untouched. */
export function moveListItem<TItem>(
    items: readonly TItem[],
    fromIndex: number,
    toIndex: number
): TItem[] {
    if (
        fromIndex === toIndex ||
        fromIndex < 0 ||
        toIndex < 0 ||
        fromIndex >= items.length ||
        toIndex >= items.length
    ) {
        return [...items];
    }

    const reordered = [...items];
    const [moved] = reordered.splice(fromIndex, 1);
    reordered.splice(toIndex, 0, moved);
    return reordered;
}
