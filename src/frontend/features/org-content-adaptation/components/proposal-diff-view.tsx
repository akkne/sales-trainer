"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import type { ContentFieldChange } from "@/features/org-content-adaptation/types/adaptation";

/**
 * One rewrite as a person reads it (O13): the model's sentence first, the changed leaves second.
 *
 * <b>The order is the point.</b> One phrase lets a РОП answer in five seconds; the leaf list is what
 * they check it against. Reversed, every item becomes a paragraph of JSON paths nobody finishes.
 *
 * <b>Nothing here is computed.</b> `changes` arrives from the server as `{path, before, after}` and
 * is rendered as it came. A client-side three-way merge of prose and grading criteria is exactly
 * what 40.18 refused to build, and this screen is one level below that refusal, not an exception
 * to it.
 */

const INITIALLY_VISIBLE_CHANGE_COUNT = 4;

interface ProposalDiffViewProps {
    changeSummary: string | null;
    changes: readonly ContentFieldChange[];
    /** The server produced a proposed body — used to tell «нет правок» apart from «нет списка правок». */
    hasProposedContent: boolean;
}

function formatFieldValue(value: string | null, absentLabel: string): string {
    if (value === null) return absentLabel;
    return value.length === 0 ? "(пусто)" : value;
}

export function ProposalDiffView({
    changeSummary,
    changes,
    hasProposedContent,
}: ProposalDiffViewProps) {
    const [isFullListShown, setIsFullListShown] = useState(false);

    const visibleChanges = isFullListShown
        ? changes
        : changes.slice(0, INITIALLY_VISIBLE_CHANGE_COUNT);
    const hiddenChangeCount = changes.length - visibleChanges.length;

    return (
        <div className="flex flex-col gap-4">
            <p className="text-sm text-ink">
                {changeSummary && changeSummary.length > 0
                    ? changeSummary
                    : "Модель не описала правку одной фразой — сравните изменённые места ниже."}
            </p>

            <div style={{ borderTop: "1px solid var(--line)" }} />

            {changes.length === 0 ? (
                <p className="text-sm text-ink-3">
                    {hasProposedContent
                        ? "Сервер не перечислил изменённые поля. Откройте упражнение в редакторе урока, чтобы сравнить самостоятельно — здесь мы ничего не досчитываем."
                        : "Предложения ещё нет: модель до этого упражнения не дошла."}
                </p>
            ) : (
                <>
                    <p className="text-xs font-medium text-ink-3 uppercase tracking-wide">
                        Изменено мест: {changes.length}
                    </p>

                    <ul className="flex flex-col gap-3">
                        {visibleChanges.map((change) => (
                            <li key={change.path} className="flex flex-col gap-1">
                                <span
                                    className="text-xs text-ink-2"
                                    style={{ fontFamily: "var(--font-mono)" }}
                                >
                                    {change.path}
                                </span>
                                <span className="text-sm text-ink-3">
                                    <span className="text-ink-4">было: </span>
                                    {formatFieldValue(change.before, "(поле добавлено)")}
                                </span>
                                <span className="text-sm text-ink">
                                    <span className="text-ink-4">стало: </span>
                                    {formatFieldValue(change.after, "(поле удалено)")}
                                </span>
                            </li>
                        ))}
                    </ul>

                    {hiddenChangeCount > 0 && (
                        <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setIsFullListShown(true)}
                            className="self-start"
                        >
                            … ещё {hiddenChangeCount}
                        </Button>
                    )}
                </>
            )}
        </div>
    );
}
