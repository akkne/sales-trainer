import type { DialogReviewNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import { isAwaitingVerdict } from "@/features/org-dialogs/lib/review-note-thread";

/**
 * The three tabs of O7 and the arithmetic behind them (docs/TENANCY/ADMIN_UI_DESIGN.md O7).
 *
 * They are computed from one unfiltered read rather than from three filtered ones — see
 * `useOrganizationReviewNotes` for why — which also makes the second half of this file possible:
 * the notification link `/admin/dialog-reviews?note={id}` names a note and not a queue, and
 * without the whole list there is nothing to look it up in.
 */

export type ReviewQueueKey = "open" | "disputes" | "notes";

export const DEFAULT_REVIEW_QUEUE_KEY: ReviewQueueKey = "open";

export const REVIEW_QUEUE_LABELS: Record<ReviewQueueKey, string> = {
    open: "Открытые",
    disputes: "Все спорные",
    notes: "Мои заметки",
};

export function selectReviewQueue(
    notes: DialogReviewNote[],
    queueKey: ReviewQueueKey
): DialogReviewNote[] {
    switch (queueKey) {
        case "open":
            return notes.filter(isAwaitingVerdict);
        case "disputes":
            return notes.filter((note) => note.kind === "score_dispute");
        case "notes":
            return notes.filter((note) => note.kind === "coaching_note");
    }
}

export function countOpenDisputes(notes: DialogReviewNote[]): number {
    return notes.filter(isAwaitingVerdict).length;
}

/**
 * What the screen shows: the active tab's queue, with the note a notification named pinned to the
 * top of it whichever queue that note actually belongs to.
 *
 * Pinned rather than «switch to the tab that contains it», for two reasons. A verdict moves its
 * note from «Открытые» to «Все спорные» the moment it is given, and a tab that follows the note
 * would move the reader somewhere they never navigated to, mid-sentence. And a link that is
 * already stale — the dispute was ruled on last week — still has to show the note it names rather
 * than an unexplained list, which pinning does and tab selection cannot.
 *
 * An id this organization has no note for is simply not found: a wrong or foreign `?note=` opens
 * the queue, not an error.
 */
export function buildVisibleReviewNotes(
    notes: DialogReviewNote[],
    queueKey: ReviewQueueKey,
    requestedNoteId: string | null
): DialogReviewNote[] {
    const queueNotes = selectReviewQueue(notes, queueKey);
    if (requestedNoteId === null) return queueNotes;

    const requestedNote = notes.find((note) => note.id === requestedNoteId);
    if (requestedNote === undefined) return queueNotes;

    return [requestedNote, ...queueNotes.filter((note) => note.id !== requestedNote.id)];
}
