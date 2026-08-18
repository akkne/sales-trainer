"use client";

import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Modal } from "@/shared/components/modal";
import { Select } from "@/shared/components/input";
import {
    AiDialogEditor,
    CategorizingEditor,
    EXERCISE_TYPES,
    FillBlankEditor,
    FindErrorEditor,
    MatchingEditor,
    MultipleChoiceEditor,
    OpenQuestionEditor,
    OrderingEditor,
    RateCallEditor,
    RewriteBetterEditor,
    TheoryCardEditor,
    emptyContentFor,
    validateExerciseContent,
    type AiDialogueContent,
    type CategorizeContent,
    type ChooseOptionContent,
    type EvaluateCallContent,
    type ExerciseContent,
    type ExerciseType,
    type FillBlankContent,
    type FreeTextContent,
    type MatchPairsContent,
    type ReorderContent,
    type RewriteContent,
    type SpotMistakeContent,
    type TheoryCardContent,
} from "@/features/admin/components/exercise-editors";
import { describeExerciseType } from "../utils/exercise-summary";
import type { AdminExercise, WriteExerciseRequest } from "../types/lesson-editor";

/**
 * The twelve editors are reused whole — same `{content, onChange}` signature, no copy and no
 * rewrite (docs/TENANCY/ADMIN_UI_DESIGN.md §4.1). Only this shell and the type names around them
 * are the organization panel's own.
 */
function renderContentEditor(
    type: ExerciseType,
    content: ExerciseContent,
    onChange: (next: ExerciseContent) => void
) {
    switch (type) {
        case "choose_option":
            return <MultipleChoiceEditor content={content as ChooseOptionContent} onChange={onChange} />;
        case "fill_blank":
            return <FillBlankEditor content={content as FillBlankContent} onChange={onChange} />;
        case "free_text":
            return <OpenQuestionEditor content={content as FreeTextContent} onChange={onChange} />;
        case "reorder":
            return <OrderingEditor content={content as ReorderContent} onChange={onChange} />;
        case "match_pairs":
            return <MatchingEditor content={content as MatchPairsContent} onChange={onChange} />;
        case "categorize":
            return <CategorizingEditor content={content as CategorizeContent} onChange={onChange} />;
        case "spot_mistake":
            return <FindErrorEditor content={content as SpotMistakeContent} onChange={onChange} />;
        case "rewrite":
            return <RewriteBetterEditor content={content as RewriteContent} onChange={onChange} />;
        case "ai_dialogue":
            return <AiDialogEditor content={content as AiDialogueContent} onChange={onChange} />;
        case "evaluate_call":
            return <RateCallEditor content={content as EvaluateCallContent} onChange={onChange} />;
        case "theory_card":
            return <TheoryCardEditor content={content as TheoryCardContent} onChange={onChange} />;
        default:
            return null;
    }
}

interface ExerciseEditorModalProps {
    open: boolean;
    /** Null when adding: the type picker is then editable and the content starts empty. */
    exercise: AdminExercise | null;
    /** Position for a new exercise; ignored when editing. */
    nextOrderInLesson: number;
    onCancel: () => void;
    onSave: (body: WriteExerciseRequest) => void;
    isPending: boolean;
    failureMessage?: string | null;
}

export function ExerciseEditorModal({ open, exercise, ...rest }: ExerciseEditorModalProps) {
    // Mounted only while open, and keyed by the row being edited, so the draft below is seeded from
    // props exactly once per opening instead of being re-synced by an effect a render later.
    return open ? (
        <OpenExerciseEditorModal key={exercise?.id ?? "new"} exercise={exercise} {...rest} />
    ) : null;
}

function OpenExerciseEditorModal({
    exercise,
    nextOrderInLesson,
    onCancel,
    onSave,
    isPending,
    failureMessage,
}: Omit<ExerciseEditorModalProps, "open">) {
    const initialType = (exercise?.type as ExerciseType) ?? "choose_option";
    const [type, setType] = useState<ExerciseType>(initialType);
    const [content, setContent] = useState<ExerciseContent>(() =>
        exercise ? (exercise.content as ExerciseContent) : emptyContentFor(initialType)
    );
    const [validationErrors, setValidationErrors] = useState<string[]>([]);

    const changeType = (nextType: ExerciseType) => {
        setType(nextType);
        setContent(emptyContentFor(nextType));
        setValidationErrors([]);
    };

    const save = () => {
        const errors = validateExerciseContent(type, content);
        setValidationErrors(errors);
        if (errors.length > 0) return;

        onSave({
            type,
            orderInLesson: exercise?.orderInLesson ?? nextOrderInLesson,
            content,
            customAiPrompt: exercise?.customAiPrompt ?? null,
        });
    };

    return (
        <Modal
            open
            onClose={isPending ? () => {} : onCancel}
            title={exercise ? "Упражнение" : "Новое упражнение"}
            size="xl"
            footer={
                <>
                    <Button variant="ghost" onClick={onCancel} disabled={isPending}>
                        Отмена
                    </Button>
                    <Button variant="primary" onClick={save} disabled={isPending}>
                        {isPending ? "Сохраняем…" : "Сохранить"}
                    </Button>
                </>
            }
        >
            <div className="flex flex-col gap-4">
                <Select
                    label="Тип упражнения"
                    value={type}
                    disabled={exercise !== null}
                    onChange={(changeEvent) => changeType(changeEvent.target.value as ExerciseType)}
                    hint={
                        exercise
                            ? "Тип существующего упражнения не меняется: попытки записаны против него."
                            : undefined
                    }
                >
                    {EXERCISE_TYPES.map((exerciseType) => (
                        <option key={exerciseType} value={exerciseType}>
                            {describeExerciseType(exerciseType)}
                        </option>
                    ))}
                </Select>

                {renderContentEditor(type, content, setContent)}

                {validationErrors.length > 0 && (
                    <ul className="rounded-xl bg-bad-soft p-3 text-sm text-bad" role="alert">
                        {validationErrors.map((error) => (
                            <li key={error}>{error}</li>
                        ))}
                    </ul>
                )}

                {failureMessage && (
                    <p className="text-sm text-bad" role="alert">
                        {failureMessage}
                    </p>
                )}
            </div>
        </Modal>
    );
}
