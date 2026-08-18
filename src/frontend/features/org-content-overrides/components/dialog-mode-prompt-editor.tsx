"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Textarea } from "@/shared/components/input";

interface DialogModePromptEditorProps {
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
    onSave: (prompts: { chatSystemPrompt: string; feedbackSystemPrompt: string }) => void;
    onCancel: () => void;
    isPending: boolean;
    failureMessage?: string | null;
}

const PROMPT_ROWS = 12;

/**
 * The third action for a dialog mode, inline on the review screen (docs/TENANCY/ADMIN_UI_DESIGN.md
 * O15). A mode has no version table and nothing to publish: saving is the whole act, and the
 * service re-points the fork marker as part of it — so the «оригинал обновился» mark clears itself.
 *
 * `key` and `bundleId` are not editable and are not shown as fields: they are the override's link
 * to the row it shadows.
 */
export function DialogModePromptEditor({
    chatSystemPrompt,
    feedbackSystemPrompt,
    onSave,
    onCancel,
    isPending,
    failureMessage,
}: DialogModePromptEditorProps) {
    // Seeded once. The editor is mounted by «Править» and unmounted on save or cancel, so there is
    // no second server value to re-sync from — and re-syncing mid-edit would overwrite typing.
    const [chatDraft, setChatDraft] = useState(chatSystemPrompt);
    const [feedbackDraft, setFeedbackDraft] = useState(feedbackSystemPrompt);

    return (
        <div className="flex flex-col gap-4">
            <Textarea
                label="Системный промпт разговора"
                hint="Что модель знает о роли, которую играет, пока продавец с ней говорит."
                rows={PROMPT_ROWS}
                className="font-mono text-xs"
                value={chatDraft}
                onChange={(changeEvent) => setChatDraft(changeEvent.target.value)}
            />
            <Textarea
                label="Системный промпт обратной связи"
                hint="По каким правилам разговор потом разбирается и оценивается."
                rows={PROMPT_ROWS}
                className="font-mono text-xs"
                value={feedbackDraft}
                onChange={(changeEvent) => setFeedbackDraft(changeEvent.target.value)}
            />

            {failureMessage && (
                <p className="text-sm text-bad" role="alert">
                    {failureMessage}
                </p>
            )}

            <div className="flex flex-wrap gap-2">
                <Button
                    variant="primary"
                    disabled={isPending}
                    onClick={() =>
                        onSave({
                            chatSystemPrompt: chatDraft,
                            feedbackSystemPrompt: feedbackDraft,
                        })
                    }
                >
                    {isPending ? "Сохраняем…" : "Сохранить"}
                </Button>
                <Button variant="ghost" onClick={onCancel} disabled={isPending}>
                    Отмена
                </Button>
            </div>
        </div>
    );
}
