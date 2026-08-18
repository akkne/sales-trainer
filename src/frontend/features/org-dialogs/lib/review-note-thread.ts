import {
    CURRENT_USER_LABEL,
    SCORE_DISPUTE_OUTCOME_LABELS,
} from "@/features/org-dialogs/constants/dialog-review-dictionary";
import type { DialogReviewNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import { resolveNoteParticipantLabel } from "@/features/org-dialogs/lib/team-member-labels";

/**
 * A disputed score is a conversation between two people, and this turns one `DialogReviewNote` row
 * into the two (or three) turns of it that a screen can read top to bottom.
 *
 * The row stores only one side as prose — `comment` — and buries the other in `resolution`,
 * `status`, `adjustedScore` and `resolvedAt`. Rendering it as a form with a filled-in answer field
 * would show the РОП their own words and hide the manager's, which is the failure mode
 * ASSIGNMENTS.md §4.1 spends four paragraphs on: the mechanism only works while both parties can
 * see that it worked.
 *
 * `side` says which end of the loop a turn came from, and `isCurrentUser` says whether that end is
 * the person reading — those two are separate facts, because a РОП routinely reads a dispute
 * another administrator ruled on.
 */

export type ReviewThreadSide = "organization" | "manager";

export interface ReviewThreadTurn {
    key: string;
    side: ReviewThreadSide;
    authorLabel: string;
    isCurrentUser: boolean;
    /** What was said. Null when the turn is an event rather than words — «прочитано», say. */
    body: string | null;
    /** A one-line statement of what the turn did, shown above or instead of the body. */
    caption: string | null;
    timestamp: string | null;
}

export interface ReviewThreadContext {
    currentUserId: string | null;
    memberNamesByUserId: Map<string, string>;
}

function labelFor(
    userId: string,
    storedDisplayName: string | null,
    context: ReviewThreadContext
): string {
    return resolveNoteParticipantLabel(
        userId,
        storedDisplayName,
        context.currentUserId,
        CURRENT_USER_LABEL,
        context.memberNamesByUserId
    );
}

/** What the verdict says in one line, including the corrected score when one was recorded. */
function describeVerdict(note: DialogReviewNote): string {
    const outcomeLabel =
        note.status === "upheld" || note.status === "rejected"
            ? SCORE_DISPUTE_OUTCOME_LABELS[note.status]
            : note.status;

    if (note.status === "upheld" && note.adjustedScore !== null) {
        return `${outcomeLabel} · справедливая оценка ${note.adjustedScore}`;
    }
    return outcomeLabel;
}

export function buildReviewNoteThread(
    note: DialogReviewNote,
    context: ReviewThreadContext
): ReviewThreadTurn[] {
    const isDispute = note.kind === "score_dispute";
    const openingSide: ReviewThreadSide = isDispute ? "manager" : "organization";

    const turns: ReviewThreadTurn[] = [
        {
            key: `${note.id}-opening`,
            side: openingSide,
            authorLabel: labelFor(note.authorUserId, note.authorDisplayName, context),
            isCurrentUser: context.currentUserId !== null && note.authorUserId === context.currentUserId,
            body: note.comment,
            caption: isDispute
                ? note.disputedScore === null
                    ? "оспаривает оценку"
                    : `оспаривает ${note.disputedScore} баллов`
                : "прислал заметку по разговору",
            timestamp: note.createdAt,
        },
    ];

    if (isDispute && note.resolvedAt !== null) {
        // `resolvedBy` is the administrator who ruled, which is not necessarily the note's author
        // and not necessarily the person reading — hence its own label rather than «Вы» by default.
        const resolverUserId = note.resolvedBy;
        turns.push({
            key: `${note.id}-verdict`,
            side: "organization",
            authorLabel:
                resolverUserId === null
                    ? "Решение"
                    : labelFor(resolverUserId, null, context),
            isCurrentUser: context.currentUserId !== null && resolverUserId === context.currentUserId,
            body: note.resolution,
            caption: describeVerdict(note),
            timestamp: note.resolvedAt,
        });
    }

    if (!isDispute && note.status === "acknowledged") {
        turns.push({
            key: `${note.id}-acknowledged`,
            side: "manager",
            authorLabel: labelFor(note.subjectUserId, note.subjectDisplayName, context),
            isCurrentUser:
                context.currentUserId !== null && note.subjectUserId === context.currentUserId,
            body: null,
            caption: "прочитал заметку",
            timestamp: note.resolvedAt,
        });
    }

    return turns;
}

/** Whether this row is still waiting on the РОП. The only rows O7's first tab shows. */
export function isAwaitingVerdict(note: DialogReviewNote): boolean {
    return note.kind === "score_dispute" && note.status === "open";
}
