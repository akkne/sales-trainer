"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import {
    BLOCKING_PRIORITY_HINT,
    OPTIONAL_ANSWER_FIELDS,
    PROFILE_GAP_PRIORITIES,
    PROFILE_GAP_PRIORITY_LABELS,
} from "../constants/profile-fields";
import type { OrganizationProfileGap } from "../types/organization-profile";
import {
    createEmptyAnswerDraft,
    validateAnswerDraft,
    type ProfileAnswerDraft,
} from "../utils/interview-answers";
import { AnswerEditor } from "./answer-editors";

interface InterviewQuestionProps {
    gap: OrganizationProfileGap;
    /** Sends exactly this one field. Rejects by throwing, which is what shows the inline error. */
    onAnswer: (draft: ProfileAnswerDraft) => Promise<void>;
    /** «Таких нет» — hides the question for this sitting only; the schema records no such answer. */
    onSkip: () => void;
    isSaving: boolean;
    /** A failure from the last attempt at *this* question. The other two are never touched. */
    saveError: string | null;
}

/**
 * One question of the interview, and the only writer of one field.
 *
 * The «Ответить» button patches a single column; it never reads the profile, splices a field in and
 * writes all seven back. That is not an optimisation — the multi-person case is the expected one
 * here, and a read-modify-write silently discards whatever a colleague answered in the meantime.
 */
export function InterviewQuestion({
    gap,
    onAnswer,
    onSkip,
    isSaving,
    saveError,
}: InterviewQuestionProps) {
    const [draft, setDraft] = useState<ProfileAnswerDraft | null>(() =>
        createEmptyAnswerDraft(gap.code)
    );
    const [validationError, setValidationError] = useState<string | null>(null);

    if (draft === null) return null;

    const priorityLabel = PROFILE_GAP_PRIORITY_LABELS[gap.priority] ?? null;
    const isBlocking = gap.priority === PROFILE_GAP_PRIORITIES.blocking;
    const canSkip = OPTIONAL_ANSWER_FIELDS.includes(gap.code);

    const submitAnswer = async () => {
        const nextValidationError = validateAnswerDraft(gap.code, draft);
        setValidationError(nextValidationError);
        if (nextValidationError) return;
        await onAnswer(draft);
    };

    const inlineError = validationError ?? saveError;

    return (
        <li className="py-5 first:pt-0 last:pb-0" data-gap-code={gap.code}>
            <div className="flex flex-wrap items-start justify-between gap-2 mb-3">
                <p className="text-sm text-ink font-medium max-w-2xl">{gap.question}</p>
                {priorityLabel && (
                    <span
                        className={`shrink-0 text-xs px-2 py-0.5 rounded-full ${
                            isBlocking ? "bg-bad-soft text-bad" : "bg-bg-2 text-ink-3"
                        }`}
                    >
                        {priorityLabel}
                    </span>
                )}
            </div>

            {isBlocking && <p className="text-xs text-ink-3 mb-3">{BLOCKING_PRIORITY_HINT}</p>}

            <AnswerEditor
                fieldCode={gap.code}
                draft={draft}
                onChange={setDraft}
                disabled={isSaving}
            />

            {inlineError && (
                <p className="mt-2 text-xs text-bad" role="alert">
                    {inlineError}
                </p>
            )}

            <div className="mt-3 flex items-center justify-end gap-2">
                {canSkip && (
                    <Button variant="ghost" size="sm" onClick={onSkip} disabled={isSaving}>
                        Таких нет
                    </Button>
                )}
                <Button
                    variant="primary"
                    size="sm"
                    loading={isSaving}
                    disabled={isSaving}
                    onClick={submitAnswer}
                >
                    {isSaving ? "Сохраняем…" : "Ответить"}
                </Button>
            </div>
        </li>
    );
}
