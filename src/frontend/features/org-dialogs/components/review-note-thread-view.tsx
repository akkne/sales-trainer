"use client";

import { Chip } from "@/shared/components/chip";
import type { DialogReviewNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import { formatDialogMomentInFull } from "@/features/org-dialogs/lib/format-dialog-moment";
import {
    buildReviewNoteThread,
    type ReviewThreadContext,
} from "@/features/org-dialogs/lib/review-note-thread";

interface ReviewNoteThreadViewProps {
    note: DialogReviewNote;
    context: ReviewThreadContext;
}

/**
 * One review note rendered as the exchange it is: the manager's side and the organization's side,
 * in the order they were written, with the reader's own turns marked.
 *
 * The two sides are told apart by which edge carries the accent bar rather than by colour alone —
 * a dispute where the reader cannot see at a glance who said what is a dispute they will rule on
 * from the wrong half of it.
 */
export function ReviewNoteThreadView({ note, context }: ReviewNoteThreadViewProps) {
    const turns = buildReviewNoteThread(note, context);

    return (
        <ol className="flex flex-col gap-3">
            {turns.map((turn) => {
                const isOrganizationSide = turn.side === "organization";
                return (
                    <li
                        key={turn.key}
                        className="pl-3"
                        style={{
                            borderLeft: `2px solid ${
                                isOrganizationSide ? "var(--primary)" : "var(--line-2)"
                            }`,
                        }}
                    >
                        <div className="flex flex-wrap items-center gap-2">
                            <span className="text-sm font-medium text-ink">{turn.authorLabel}</span>
                            {turn.isCurrentUser && (
                                <Chip tone="ghost" size="sm">
                                    это вы
                                </Chip>
                            )}
                            {turn.caption && (
                                <span className="text-sm text-ink-3">{turn.caption}</span>
                            )}
                            {turn.timestamp && (
                                <span className="text-xs text-ink-4 ml-auto">
                                    {formatDialogMomentInFull(turn.timestamp)}
                                </span>
                            )}
                        </div>
                        {turn.body && (
                            <p className="mt-1 text-sm text-ink-2 whitespace-pre-wrap">{turn.body}</p>
                        )}
                    </li>
                );
            })}
        </ol>
    );
}
