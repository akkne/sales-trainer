"use client";

import { Button, IconButton } from "@/shared/components/button";
import { EmptyState } from "@/shared/components/empty-state";
import { describeExerciseType, summarizeExerciseContent } from "../utils/exercise-summary";
import type { AdminExercise } from "../types/lesson-editor";

interface ExerciseListProps {
    exercises: AdminExercise[];
    readOnly: boolean;
    onEdit: (exercise: AdminExercise) => void;
    onDelete: (exercise: AdminExercise) => void;
    onMove: (fromIndex: number, toIndex: number) => void;
    onAdd: () => void;
    isReordering: boolean;
}

/**
 * The body of a lesson is its exercises — the lesson row itself carries only a title
 * (docs/TENANCY/CONTENT_MODEL.md §0), so this list is what «редактировать урок» actually means.
 *
 * Reordering is two buttons rather than a drag surface: `PUT /admin/exercises/{id}` takes one
 * exercise at a time and there is no batch reorder route, so every move is a pair of writes and
 * making it feel continuous would misrepresent what is happening.
 */
export function ExerciseList({
    exercises,
    readOnly,
    onEdit,
    onDelete,
    onMove,
    onAdd,
    isReordering,
}: ExerciseListProps) {
    if (exercises.length === 0) {
        return (
            <EmptyState
                icon="layers"
                title="В уроке пока нет упражнений"
                description={
                    readOnly
                        ? "Это урок из общей библиотеки — упражнения в нём появятся, когда их добавит Sellevate."
                        : "Добавьте первое: именно упражнения команда и решает, у самого урока текста нет."
                }
                action={
                    readOnly ? undefined : (
                        <Button variant="primary" onClick={onAdd}>
                            + упражнение
                        </Button>
                    )
                }
            />
        );
    }

    return (
        <div className="flex flex-col gap-2">
            {exercises.map((exercise, index) => {
                const preview = summarizeExerciseContent(exercise.type, exercise.content);

                return (
                    <div
                        key={exercise.id}
                        className="flex items-center gap-3 rounded-xl border border-line px-3 py-2.5"
                    >
                        <span
                            className="w-6 shrink-0 text-right text-sm text-ink-3"
                            style={{ fontFamily: "var(--font-mono)" }}
                        >
                            {index + 1}
                        </span>
                        <div className="min-w-0 flex-1">
                            <p className="truncate text-sm text-ink">
                                {describeExerciseType(exercise.type)}
                                {preview && <span className="text-ink-3"> · {preview}</span>}
                            </p>
                        </div>
                        {!readOnly && (
                            <div className="flex shrink-0 items-center gap-1">
                                <IconButton
                                    icon="arrow-up"
                                    aria-label="Выше"
                                    variant="ghost"
                                    size="sm"
                                    disabled={index === 0 || isReordering}
                                    onClick={() => onMove(index, index - 1)}
                                />
                                <IconButton
                                    icon="chevron-down"
                                    aria-label="Ниже"
                                    variant="ghost"
                                    size="sm"
                                    disabled={index === exercises.length - 1 || isReordering}
                                    onClick={() => onMove(index, index + 1)}
                                />
                                <IconButton
                                    icon="edit"
                                    aria-label="Править"
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => onEdit(exercise)}
                                />
                                <IconButton
                                    icon="delete"
                                    aria-label="Удалить"
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => onDelete(exercise)}
                                />
                            </div>
                        )}
                    </div>
                );
            })}
        </div>
    );
}
