/**
 * Selecting the three lines a coaching note is about (docs/TENANCY/ADMIN_UI_DESIGN.md O6).
 *
 * Everything here works on the server's `index`, never on a position in the array. That is part of
 * the transcript contract (`AdminDialogTranscriptMessageDto`): the same sentence said twice has to
 * stay separately quotable, and a note that points at «the fifth element of whatever came back»
 * points at nothing once anything about the response changes.
 */

export interface TranscriptSelection {
    fromIndex: number;
    toIndex: number;
}

export interface SelectableTranscriptMessage {
    index: number;
    content: string;
}

/**
 * A click selects one line; a click on the only selected line clears it; shift+click stretches the
 * selection from where it started to where it was clicked, in either direction.
 *
 * Returns the new selection, or null for «nothing is selected» — the same value the composer reads
 * to know it has nothing to send.
 */
export function toggleTranscriptSelection(
    currentSelection: TranscriptSelection | null,
    messageIndex: number,
    isRangeExtension: boolean
): TranscriptSelection | null {
    if (isRangeExtension && currentSelection !== null) {
        const anchorIndex = currentSelection.fromIndex;
        return {
            fromIndex: Math.min(anchorIndex, messageIndex),
            toIndex: Math.max(anchorIndex, messageIndex),
        };
    }

    const isOnlySelectedMessage =
        currentSelection !== null &&
        currentSelection.fromIndex === messageIndex &&
        currentSelection.toIndex === messageIndex;

    return isOnlySelectedMessage ? null : { fromIndex: messageIndex, toIndex: messageIndex };
}

export function isMessageSelected(
    selection: TranscriptSelection | null,
    messageIndex: number
): boolean {
    if (selection === null) return false;
    return messageIndex >= selection.fromIndex && messageIndex <= selection.toIndex;
}

export function countSelectedMessages(
    messages: SelectableTranscriptMessage[],
    selection: TranscriptSelection | null
): number {
    if (selection === null) return 0;
    return messages.filter((message) => isMessageSelected(selection, message.index)).length;
}

/**
 * The lines themselves, joined in transcript order.
 *
 * `quotedText` is copied into the note row rather than referenced (ASSIGNMENTS.md §4.1), so what
 * this builds is the whole of what the manager will read next month — including after retention
 * has trimmed the session it came from. Speaker labels are left out: the quote is a fragment of
 * that person's own conversation, and the indexes travel alongside for anyone who needs the
 * context back.
 */
export function buildQuotedText(
    messages: SelectableTranscriptMessage[],
    selection: TranscriptSelection | null
): string {
    if (selection === null) return "";
    return messages
        .filter((message) => isMessageSelected(selection, message.index))
        .map((message) => message.content)
        .join("\n")
        .trim();
}

/** «Выделено: реплики 5–6» / «Выделено: реплика 5», or null when nothing is selected. */
export function describeSelectedRange(selection: TranscriptSelection | null): string | null {
    if (selection === null) return null;
    return selection.fromIndex === selection.toIndex
        ? `реплика ${selection.fromIndex}`
        : `реплики ${selection.fromIndex}–${selection.toIndex}`;
}

/**
 * The same phrase for a note that already exists, built from the two nullable indexes the DTO
 * carries. A manager's dispute may quote nothing at all, and then there is no range to describe.
 */
export function describeQuotedRange(
    quotedFromMessageIndex: number | null,
    quotedToMessageIndex: number | null
): string | null {
    if (quotedFromMessageIndex === null) return null;
    return describeSelectedRange({
        fromIndex: quotedFromMessageIndex,
        toIndex: quotedToMessageIndex ?? quotedFromMessageIndex,
    });
}
