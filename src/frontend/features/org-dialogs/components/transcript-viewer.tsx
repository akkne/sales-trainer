"use client";

import type { DialogTranscriptMessage } from "@/features/org-dialogs/hooks/use-dialog-transcript";
import {
    isMessageSelected,
    type TranscriptSelection,
} from "@/features/org-dialogs/lib/transcript-selection";

interface TranscriptViewerProps {
    messages: DialogTranscriptMessage[];
    /** The manager's name, used as the speaker label for their own lines. */
    managerLabel: string;
    selection: TranscriptSelection | null;
    onMessageClick: (messageIndex: number, isRangeExtension: boolean) => void;
}

/** `DialogMessageRoles.Assistant` is the roleplay client; everything else is the person training. */
const CLIENT_ROLE = "assistant";

/**
 * The left column of O6: the conversation, with the fragment the note will quote selected in it.
 *
 * Every line carries the server's `index` and is keyed and reported by it, never by its position
 * in the array — the same sentence said twice has to stay separately quotable
 * (`AdminDialogTranscriptMessageDto`).
 *
 * A line is a button: clicking selects it, clicking it again clears the selection, and
 * shift+clicking stretches the selection to it. Buttons rather than a text selection because the
 * quote travels as indexes plus a copied string, and a browser text range cannot say which lines
 * it crossed.
 */
export function TranscriptViewer({
    messages,
    managerLabel,
    selection,
    onMessageClick,
}: TranscriptViewerProps) {
    return (
        <ol className="flex flex-col gap-1">
            {messages.map((message) => {
                const isSelected = isMessageSelected(selection, message.index);
                const isClient = message.role === CLIENT_ROLE;

                return (
                    <li key={message.index}>
                        <button
                            type="button"
                            aria-pressed={isSelected}
                            onClick={(clickEvent) =>
                                onMessageClick(message.index, clickEvent.shiftKey)
                            }
                            className="w-full text-left rounded-xl px-3 py-2 transition-colors"
                            style={{
                                background: isSelected ? "var(--primary-soft)" : "transparent",
                                border: `1px solid ${isSelected ? "var(--primary)" : "transparent"}`,
                            }}
                        >
                            <span className="flex items-baseline gap-3">
                                <span
                                    className="tnum text-xs text-ink-4 shrink-0"
                                    style={{ fontFamily: "var(--font-mono)", minWidth: "1.5rem" }}
                                >
                                    {message.index}
                                </span>
                                <span className="min-w-0">
                                    <span className="block text-xs font-medium text-ink-3">
                                        {isClient ? "Клиент" : managerLabel}
                                    </span>
                                    <span className="block text-sm text-ink-2 whitespace-pre-wrap">
                                        {message.content}
                                    </span>
                                </span>
                            </span>
                        </button>
                    </li>
                );
            })}
        </ol>
    );
}
