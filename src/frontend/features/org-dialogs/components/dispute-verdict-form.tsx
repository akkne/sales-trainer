"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { TextInput, Textarea } from "@/shared/components/input";
import { ApiError } from "@/shared/api/api-client";
import { ADJUSTED_SCORE_DISCLAIMER, SCORE_DISPUTE_OUTCOME_LABELS } from "@/features/org-dialogs/constants/dialog-review-dictionary";
import { useResolveScoreDispute } from "@/features/org-dialogs/hooks/use-dialog-review-notes";
import {
    buildResolveDisputeRequest,
    EMPTY_DISPUTE_VERDICT_DRAFT,
    validateDisputeVerdict,
    type DisputeVerdictDraft,
} from "@/features/org-dialogs/lib/dispute-verdict";

interface DisputeVerdictFormProps {
    noteId: string;
}

const VERDICT_FAILURE_MESSAGE = "Не удалось вынести решение. Попробуйте ещё раз.";

/**
 * The verdict on one dispute (docs/TENANCY/ADMIN_UI_DESIGN.md O7).
 *
 * Two asymmetries are deliberate and both come from `DialogReviewService`. Rejecting requires
 * words — «Оценка остаётся в силе, потому что…» is the sentence that keeps the mechanism from
 * being a rubber stamp — and agreeing does not. And the corrected score exists only on an
 * agreement, always under the caption saying it changes nothing: the number is recorded as
 * labelled data, and 40.22 recomputes every grade from attempt rows, so a hand-edited score would
 * be overwritten by the next event even if the server tried.
 */
export function DisputeVerdictForm({ noteId }: DisputeVerdictFormProps) {
    const [draft, setDraft] = useState<DisputeVerdictDraft>(EMPTY_DISPUTE_VERDICT_DRAFT);
    const resolveScoreDispute = useResolveScoreDispute();

    const validation = validateDisputeVerdict(draft);

    const failureMessage =
        resolveScoreDispute.error instanceof ApiError
            ? resolveScoreDispute.error.message
            : resolveScoreDispute.isError
              ? VERDICT_FAILURE_MESSAGE
              : null;

    const submit = () => {
        const request = buildResolveDisputeRequest(noteId, draft);
        if (request === null) return;
        resolveScoreDispute.mutate(request);
    };

    return (
        <div className="mt-4 pt-4" style={{ borderTop: "1px solid var(--line)" }}>
            <fieldset className="flex flex-col gap-3">
                <legend className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-2">
                    Ваше решение
                </legend>

                <label className="flex items-center gap-2 text-sm text-ink-2">
                    <input
                        type="radio"
                        name={`verdict-${noteId}`}
                        checked={draft.outcome === "upheld"}
                        onChange={() => setDraft({ ...draft, outcome: "upheld" })}
                    />
                    {SCORE_DISPUTE_OUTCOME_LABELS.upheld}
                </label>

                {draft.outcome === "upheld" && (
                    <div className="pl-6">
                        <TextInput
                            label="Справедливая оценка"
                            inputSize="sm"
                            inputMode="numeric"
                            placeholder="необязательно"
                            value={draft.adjustedScore}
                            error={validation.adjustedScoreError ?? undefined}
                            hint={ADJUSTED_SCORE_DISCLAIMER}
                            onChange={(changeEvent) =>
                                setDraft({ ...draft, adjustedScore: changeEvent.target.value })
                            }
                        />
                    </div>
                )}

                <label className="flex items-center gap-2 text-sm text-ink-2">
                    <input
                        type="radio"
                        name={`verdict-${noteId}`}
                        checked={draft.outcome === "rejected"}
                        onChange={() => setDraft({ ...draft, outcome: "rejected" })}
                    />
                    {SCORE_DISPUTE_OUTCOME_LABELS.rejected}
                </label>
            </fieldset>

            {draft.outcome !== null && (
                <div className="mt-3">
                    <Textarea
                        label={draft.outcome === "rejected" ? "Почему" : "Комментарий"}
                        required={draft.outcome === "rejected"}
                        rows={3}
                        value={draft.resolution}
                        error={validation.resolutionError ?? undefined}
                        placeholder={
                            draft.outcome === "rejected"
                                ? "Оценка остаётся в силе, потому что…"
                                : "необязательно"
                        }
                        onChange={(changeEvent) =>
                            setDraft({ ...draft, resolution: changeEvent.target.value })
                        }
                    />
                </div>
            )}

            {failureMessage && (
                <p role="alert" className="mt-2 text-sm" style={{ color: "var(--bad)" }}>
                    {failureMessage}
                </p>
            )}

            <div className="mt-3 flex justify-end">
                <Button
                    variant="primary"
                    onClick={submit}
                    disabled={!validation.canSubmit}
                    loading={resolveScoreDispute.isPending}
                >
                    Вынести решение
                </Button>
            </div>
        </div>
    );
}
