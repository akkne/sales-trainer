"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { Card, CardContent } from "@/shared/components/card";
import { Chip } from "@/shared/components/chip";
import { Icon } from "@/shared/components/icon";
import {
    describeDialogReviewKind,
    describeDialogReviewStatus,
    resolveDialogReviewStatusTone,
} from "@/features/org-dialogs/constants/dialog-review-dictionary";
import type { DialogReviewNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import { describeQuotedRange } from "@/features/org-dialogs/lib/transcript-selection";
import {
    isAwaitingVerdict,
    type ReviewThreadContext,
} from "@/features/org-dialogs/lib/review-note-thread";
import { DisputeVerdictForm } from "@/features/org-dialogs/components/dispute-verdict-form";
import { ReviewNoteThreadView } from "@/features/org-dialogs/components/review-note-thread-view";

interface ReviewNoteCardProps {
    note: DialogReviewNote;
    context: ReviewThreadContext;
    /** True for the note a notification link named: expanded, outlined and scrolled to. */
    isHighlighted: boolean;
}

/**
 * One row of O7's queue.
 *
 * The card leads with the thread rather than with a form. A disputed score is an argument between
 * two people (ASSIGNMENTS.md §4.1), and a screen that opens on the verdict controls with the
 * complaint folded away underneath asks the РОП to rule before reading.
 *
 * The quote is folded by default and unfolded for the highlighted note, which is the difference
 * between a queue that can be scanned and a wall of transcript fragments.
 */
export function ReviewNoteCard({ note, context, isHighlighted }: ReviewNoteCardProps) {
    const [isQuoteVisible, setIsQuoteVisible] = useState(isHighlighted);
    const cardElementRef = useRef<HTMLDivElement | null>(null);

    // The quote starts unfolded for this card through the initial state above; the effect only
    // moves the viewport, which is exactly the kind of thing an effect is for.
    useEffect(() => {
        if (isHighlighted) {
            cardElementRef.current?.scrollIntoView({ block: "center" });
        }
    }, [isHighlighted]);

    const quotedRangeLabel = describeQuotedRange(
        note.quotedFromMessageIndex,
        note.quotedToMessageIndex
    );

    return (
        <div ref={cardElementRef} className="mb-4">
            <Card
                style={
                    isHighlighted ? { outline: "2px solid var(--primary)" } : undefined
                }
            >
                <CardContent style={{ marginTop: 0 }}>
                    <div className="flex flex-wrap items-center gap-2 mb-3">
                        <Chip tone={resolveDialogReviewStatusTone(note.status)} size="sm">
                            {describeDialogReviewStatus(note.status)}
                        </Chip>
                        <span className="text-sm text-ink-3">
                            {describeDialogReviewKind(note.kind)}
                        </span>
                        {/* `DialogReviewNoteDto` carries the mode *key* and no title — the panel has
                            no endpoint that names an organization's dialog modes. Shown raw rather
                            than prettified into something the backend never said. */}
                        <span className="text-sm text-ink-3">· {note.dialogModeKey}</span>
                    </div>

                    <ReviewNoteThreadView note={note} context={context} />

                    {note.quotedText && (
                        <div className="mt-3">
                            <button
                                type="button"
                                onClick={() => setIsQuoteVisible(!isQuoteVisible)}
                                className="text-xs text-ink-3 hover:text-ink transition-colors"
                            >
                                {isQuoteVisible ? "Скрыть цитату" : "Показать цитату"}
                                {quotedRangeLabel ? ` (${quotedRangeLabel})` : ""}
                            </button>

                            {isQuoteVisible && (
                                <blockquote
                                    className="mt-2 rounded-xl px-3 py-2 text-sm text-ink-2 whitespace-pre-wrap"
                                    style={{ background: "var(--bg-2)" }}
                                >
                                    {note.quotedText}
                                </blockquote>
                            )}
                        </div>
                    )}

                    <div className="mt-3">
                        <Link
                            href={`/org/dialogs/${encodeURIComponent(note.sessionId)}`}
                            className="inline-flex items-center gap-1.5 text-sm text-ink-3 hover:text-ink transition-colors"
                        >
                            Открыть разговор
                            <Icon name="arrow-right" size="sm" />
                        </Link>
                    </div>

                    {isAwaitingVerdict(note) && <DisputeVerdictForm noteId={note.id} />}
                </CardContent>
            </Card>
        </div>
    );
}
