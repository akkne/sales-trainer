"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Textarea } from "@/shared/components/input";
import { ApiError } from "@/shared/api/api-client";
import type { DialogTranscriptMessage } from "@/features/org-dialogs/hooks/use-dialog-transcript";
import { useCreateCoachingNote } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import {
    buildQuotedText,
    describeSelectedRange,
    type TranscriptSelection,
} from "@/features/org-dialogs/lib/transcript-selection";

interface ReviewNoteComposerProps {
    sessionId: string;
    messages: DialogTranscriptMessage[];
    selection: TranscriptSelection | null;
    onSent: () => void;
}

const SEND_FAILURE_MESSAGE = "Не удалось отправить заметку. Попробуйте ещё раз.";

/**
 * «РАЗБОР» — the right column of O6 (docs/TENANCY/ADMIN_UI_DESIGN.md O6).
 *
 * Both fields are required, and for different reasons. `comment` because a fragment sent without
 * words is a reprimand with no content; `quotedText` because the server copies the lines into the
 * row instead of referencing them, and a note that renders empty in six months — the session aged
 * out of Mongo — failed at the one moment it existed for.
 *
 * The two requirements are stated as sentences under a disabled button rather than as errors after
 * a click: there is nothing to discover here, only something to do first.
 */
export function ReviewNoteComposer({
    sessionId,
    messages,
    selection,
    onSent,
}: ReviewNoteComposerProps) {
    const [comment, setComment] = useState("");
    const createCoachingNote = useCreateCoachingNote();

    const quotedText = buildQuotedText(messages, selection);
    const selectedRangeLabel = describeSelectedRange(selection);
    const trimmedComment = comment.trim();
    const canSend = quotedText.length > 0 && trimmedComment.length > 0;

    const failureMessage =
        createCoachingNote.error instanceof ApiError
            ? createCoachingNote.error.message
            : createCoachingNote.isError
              ? SEND_FAILURE_MESSAGE
              : null;

    const send = () => {
        if (!canSend || selection === null) return;

        createCoachingNote.mutate(
            {
                sessionId,
                quotedFromMessageIndex: selection.fromIndex,
                quotedToMessageIndex: selection.toIndex,
                quotedText,
                comment: trimmedComment,
            },
            {
                onSuccess: () => {
                    setComment("");
                    onSent();
                },
            }
        );
    };

    return (
        <section>
            <h2 className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-3">Разбор</h2>

            {selectedRangeLabel === null ? (
                <p className="text-sm text-ink-3 mb-4">
                    Выделите реплику в транскрипте слева, чтобы процитировать её. Shift+клик
                    расширяет выделение на несколько реплик подряд.
                </p>
            ) : (
                <>
                    <p className="text-sm text-ink-3 mb-2">Выделено: {selectedRangeLabel}</p>
                    <blockquote
                        className="rounded-xl px-3 py-2 mb-4 text-sm text-ink-2 whitespace-pre-wrap"
                        style={{ background: "var(--bg-2)" }}
                    >
                        {quotedText}
                    </blockquote>
                </>
            )}

            <Textarea
                label="Комментарий"
                required
                rows={4}
                value={comment}
                placeholder="Скидка отдана до того, как выяснили объём."
                onChange={(changeEvent) => setComment(changeEvent.target.value)}
            />

            {failureMessage && (
                <p role="alert" className="mt-2 text-sm" style={{ color: "var(--bad)" }}>
                    {failureMessage}
                </p>
            )}

            <div className="mt-3">
                <Button
                    variant="primary"
                    onClick={send}
                    disabled={!canSend}
                    loading={createCoachingNote.isPending}
                >
                    Отправить менеджеру
                </Button>
            </div>
        </section>
    );
}
