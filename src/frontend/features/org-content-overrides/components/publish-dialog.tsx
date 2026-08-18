"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { PUBLISH_SCOPE_OPTIONS } from "../constants/override-dictionary";

interface PublishDialogProps {
    open: boolean;
    onCancel: () => void;
    onConfirm: (isBreaking: boolean) => void;
    isPending: boolean;
    /** The server's answer when nothing actually changed, or a failure to report. */
    notice?: string | null;
}

/**
 * One mandatory question with no default (docs/TENANCY/ADMIN_UI_DESIGN.md O19).
 *
 * `isBreaking` cannot be derived and pre-selecting either option would answer it for the person who
 * actually knows: a corrected comma and a moved correct answer are the same diff, and the chart
 * either continues or breaks on the difference.
 */
export function PublishDialog({ open, ...rest }: PublishDialogProps) {
    // Mounted only while open, so every opening starts with the question unanswered. A `useEffect`
    // that reset the choice would do the same thing one render later and one lint rule worse.
    return open ? <OpenPublishDialog {...rest} /> : null;
}

function OpenPublishDialog({
    onCancel,
    onConfirm,
    isPending,
    notice,
}: Omit<PublishDialogProps, "open">) {
    const [chosenScope, setChosenScope] = useState<boolean | null>(null);

    return (
        <Modal
            open
            onClose={isPending ? () => {} : onCancel}
            title="Что вы поменяли?"
            size="md"
            footer={
                <>
                    <Button variant="ghost" onClick={onCancel} disabled={isPending}>
                        Отмена
                    </Button>
                    <Button
                        variant="primary"
                        onClick={() => chosenScope !== null && onConfirm(chosenScope)}
                        disabled={chosenScope === null || isPending}
                    >
                        {isPending ? "Публикуем…" : "Опубликовать"}
                    </Button>
                </>
            }
        >
            <fieldset className="flex flex-col gap-3">
                <legend className="sr-only">Характер правки</legend>
                {PUBLISH_SCOPE_OPTIONS.map((option) => (
                    <label
                        key={String(option.isBreaking)}
                        className="flex cursor-pointer gap-3 rounded-xl border border-line p-3 hover:bg-bg-2"
                    >
                        <input
                            type="radio"
                            name="publish-scope"
                            className="mt-1"
                            checked={chosenScope === option.isBreaking}
                            onChange={() => setChosenScope(option.isBreaking)}
                        />
                        <span className="min-w-0">
                            <span className="block text-sm text-ink">{option.label}</span>
                            <span className="mt-0.5 block text-xs text-ink-3">{option.description}</span>
                        </span>
                    </label>
                ))}
            </fieldset>

            {notice && (
                <p className="mt-4 text-sm text-ink-2" role="status">
                    {notice}
                </p>
            )}
        </Modal>
    );
}
