import type { AssignmentContentKind } from "@/features/assignments/utils/completion-rule";
import type {
    AssignmentContentItem,
    AssignmentPersona,
} from "@/features/org-assignments/types/assignment";

/**
 * A content item while it is being assembled.
 *
 * It carries the title the picker knew at the moment of choosing, which the API never stores and
 * never returns — `AssignmentDto.content` is `(kind, reference, orderIndex, persona)` and nothing
 * more. The title is display-only and is dropped on the way to the server.
 */
export interface AssignmentContentDraftItem {
    kind: AssignmentContentKind;
    reference: string;
    title: string | null;
    persona: AssignmentPersona | null;
}

export const EMPTY_PERSONA: AssignmentPersona = {
    name: null,
    position: null,
    personality: null,
    difficulty: null,
};

/**
 * `orderIndex` is the array position and is never typed by a human: the server re-packs the indices
 * densely in arrival order anyway, so a number field would only be a second source of truth.
 *
 * A persona is kept on `dialog_scenario` alone — the server drops it from every other kind, and
 * sending it anyway would make the saved assignment differ from the one on screen.
 */
export function toContentItems(
    draftItems: AssignmentContentDraftItem[]
): AssignmentContentItem[] {
    return draftItems.map((draftItem, index) => ({
        kind: draftItem.kind,
        reference: draftItem.reference,
        orderIndex: index,
        persona: draftItem.kind === "dialog_scenario" ? draftItem.persona : null,
    }));
}

export function toContentDraftItems(
    items: AssignmentContentItem[]
): AssignmentContentDraftItem[] {
    return [...items]
        .sort((left, right) => left.orderIndex - right.orderIndex)
        .map((item) => ({
            kind: item.kind,
            reference: item.reference,
            title: null,
            persona: item.persona ?? null,
        }));
}

/** `(kind, reference)` is unique per assignment — the server answers 400 for a repeat. */
export function containsContentItem(
    draftItems: AssignmentContentDraftItem[],
    kind: AssignmentContentKind,
    reference: string
): boolean {
    return draftItems.some((item) => item.kind === kind && item.reference === reference);
}

export function moveContentItem(
    draftItems: AssignmentContentDraftItem[],
    fromIndex: number,
    toIndex: number
): AssignmentContentDraftItem[] {
    if (
        fromIndex === toIndex ||
        fromIndex < 0 ||
        toIndex < 0 ||
        fromIndex >= draftItems.length ||
        toIndex >= draftItems.length
    ) {
        return draftItems;
    }

    const reordered = [...draftItems];
    const [moved] = reordered.splice(fromIndex, 1);
    reordered.splice(toIndex, 0, moved);

    return reordered;
}

export function collectContentKinds(
    draftItems: AssignmentContentDraftItem[]
): AssignmentContentKind[] {
    return Array.from(new Set(draftItems.map((item) => item.kind)));
}
