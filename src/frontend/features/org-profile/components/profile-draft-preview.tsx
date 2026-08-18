"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Card, CardContent } from "@/shared/components/card";
import { Checkbox } from "@/shared/components/input";
import { ErrorState } from "@/shared/components/error-state";
import { SkeletonList } from "@/shared/components/skeleton";
import { PROFILE_FIELD_LABELS } from "../constants/profile-fields";
import type {
    OrganizationProfileDraftPreview,
    OrganizationProfileFieldProposal,
} from "../types/organization-profile";
import { groupDraftProposals, isDraftWithoutEffect, toggleAcceptedField } from "../utils/draft-preview";
import { formatQuestionCount } from "../utils/russian-counts";

interface ProfileDraftPreviewProps {
    preview: OrganizationProfileDraftPreview | null;
    isLoading: boolean;
    loadError: string | null;
    isApplying: boolean;
    applyError: string | null;
    onApply: (acceptedFields: string[]) => void;
    onCancel: () => void;
}

/**
 * «Что ИИ прочитал в ваших материалах» — the one screen between an extracted structure and the
 * profile every lesson reads from.
 *
 * The conflict checkboxes start **unticked**, and that is the whole point of the screen. A preview
 * that arrives with the conflicts pre-selected is a silent overwrite with a checkbox drawn on it,
 * and the thing on the other side of that overwrite is a positioning sentence somebody argued about
 * for a week. There is also no way to delete a banned claim from here — the merge is add-only, and
 * removing one is a decision made on the full form while looking at the whole list.
 */
export function ProfileDraftPreview({
    preview,
    isLoading,
    loadError,
    isApplying,
    applyError,
    onApply,
    onCancel,
}: ProfileDraftPreviewProps) {
    const [acceptedFields, setAcceptedFields] = useState<string[]>([]);

    if (isLoading) {
        return (
            <Card>
                <CardContent>
                    <SkeletonList count={4} rowHeight={44} />
                </CardContent>
            </Card>
        );
    }

    if (loadError || !preview) {
        return (
            <Card>
                <CardContent>
                    <ErrorState
                        title="Не удалось разобрать структуру"
                        message={
                            loadError ??
                            "Материалы не дошли до этого экрана. Откройте прогон в разделе «Контент» и повторите перенос."
                        }
                        compact
                    />
                    <div className="flex justify-center">
                        <Button variant="secondary" onClick={onCancel}>
                            Вернуться к профилю
                        </Button>
                    </div>
                </CardContent>
            </Card>
        );
    }

    const groups = groupDraftProposals(preview.fields);
    const hasNothingToApply = isDraftWithoutEffect(preview.fields);
    const remainingQuestionCount = preview.gapsAfterApply.totalGapCount;

    return (
        <Card>
            <CardContent className="space-y-6">
                {hasNothingToApply ? (
                    <p className="text-sm text-ink-3">
                        В этих материалах нет ничего, чего профиль ещё не знает. Применять нечего.
                    </p>
                ) : (
                    <>
                        {groups.filled.length > 0 && (
                            <section>
                                <h3 className="text-xs uppercase tracking-wide text-ink-3 mb-2">
                                    Заполнится
                                </h3>
                                <ul className="space-y-2">
                                    {groups.filled.map((proposal) => (
                                        <FilledRow key={proposal.field} proposal={proposal} />
                                    ))}
                                </ul>
                            </section>
                        )}

                        {groups.extended.length > 0 && (
                            <section>
                                <h3 className="text-xs uppercase tracking-wide text-ink-3 mb-2">
                                    Дополнится
                                </h3>
                                <ul className="space-y-1">
                                    {groups.extended.map((proposal) => (
                                        <li
                                            key={proposal.field}
                                            className="flex justify-between gap-4 text-sm"
                                        >
                                            <span className="text-ink">
                                                {PROFILE_FIELD_LABELS[proposal.field] ??
                                                    proposal.field}
                                            </span>
                                            <span className="text-ink-3 font-mono tabular-nums shrink-0">
                                                + {proposal.addedItemCount}
                                            </span>
                                        </li>
                                    ))}
                                </ul>
                                <p className="mt-2 text-xs text-ink-3">
                                    Списки только пополняются — то, что вы вписали раньше, остаётся
                                    на месте.
                                </p>
                            </section>
                        )}

                        {groups.conflicting.length > 0 && (
                            <section>
                                <h3 className="text-xs uppercase tracking-wide text-ink-3 mb-2">
                                    Расхождение — решать вам ({groups.conflicting.length})
                                </h3>
                                <ul className="space-y-3">
                                    {groups.conflicting.map((proposal) => (
                                        <li
                                            key={proposal.field}
                                            className="rounded-xl border border-line p-3"
                                        >
                                            <Checkbox
                                                label={
                                                    PROFILE_FIELD_LABELS[proposal.field] ??
                                                    proposal.field
                                                }
                                                checked={acceptedFields.includes(proposal.field)}
                                                disabled={isApplying}
                                                onChange={() =>
                                                    setAcceptedFields((current) =>
                                                        toggleAcceptedField(
                                                            current,
                                                            proposal.field
                                                        )
                                                    )
                                                }
                                            />
                                            <dl className="mt-2 space-y-1 text-sm">
                                                <div className="flex gap-2">
                                                    <dt className="w-16 shrink-0 text-ink-3">
                                                        сейчас:
                                                    </dt>
                                                    <dd className="text-ink min-w-0">
                                                        {proposal.currentValue}
                                                    </dd>
                                                </div>
                                                <div className="flex gap-2">
                                                    <dt className="w-16 shrink-0 text-ink-3">
                                                        ИИ:
                                                    </dt>
                                                    <dd className="text-ink min-w-0">
                                                        {proposal.suggestedValue}
                                                    </dd>
                                                </div>
                                            </dl>
                                        </li>
                                    ))}
                                </ul>
                                <p className="mt-2 text-xs text-ink-3">
                                    Ничего не заменится, пока вы не поставите галочку.
                                </p>
                            </section>
                        )}
                    </>
                )}

                <p className="text-sm text-ink-3">
                    {remainingQuestionCount === 0
                        ? "После применения вопросов не останется."
                        : `После применения останется ${formatQuestionCount(remainingQuestionCount)}.`}
                </p>

                {applyError && (
                    <p className="text-xs text-bad" role="alert">
                        {applyError}
                    </p>
                )}

                <div className="flex flex-wrap justify-end gap-2">
                    <Button variant="secondary" onClick={onCancel} disabled={isApplying}>
                        Отмена
                    </Button>
                    <Button
                        variant="primary"
                        loading={isApplying}
                        disabled={isApplying || hasNothingToApply}
                        onClick={() => onApply(acceptedFields)}
                    >
                        Применить
                    </Button>
                </div>
            </CardContent>
        </Card>
    );
}

function FilledRow({ proposal }: { proposal: OrganizationProfileFieldProposal }) {
    return (
        <li className="text-sm">
            <span className="text-ink-3">
                {PROFILE_FIELD_LABELS[proposal.field] ?? proposal.field}
            </span>
            <span className="text-ink-4" aria-hidden>
                {" → "}
            </span>
            <span className="text-ink">{proposal.suggestedValue}</span>
        </li>
    );
}
