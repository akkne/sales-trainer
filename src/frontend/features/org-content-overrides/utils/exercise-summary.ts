/**
 * One line describing an exercise in the O19 list, without opening its editor.
 *
 * The twelve editors are reused whole from `features/admin/components/exercise-editors`; only the
 * summary is written here, because the platform panel's own preview is English and lives inside its
 * page component.
 */

import { TYPE_LABELS, type ExerciseType } from "@/features/admin/components/exercise-editors";

/**
 * Russian names for the exercise types, for the row and the «+ упражнение» picker. The platform
 * panel's `TYPE_LABELS` stays English — it is a different panel for a different audience
 * (docs/TENANCY/ADMIN_UI_DESIGN.md decision 3).
 */
export const RUSSIAN_EXERCISE_TYPE_LABELS: Record<ExerciseType, string> = {
    choose_option: "Выбор варианта",
    fill_blank: "Пропуск в тексте",
    reorder: "Порядок шагов",
    match_pairs: "Пары",
    categorize: "Распределение по группам",
    spot_mistake: "Найти ошибку",
    rewrite: "Переписать лучше",
    ai_dialogue: "Диалог с ИИ",
    evaluate_call: "Оценить звонок",
    free_text: "Свободный ответ",
    theory_card: "Теория",
};

export function describeExerciseType(type: string): string {
    return (
        RUSSIAN_EXERCISE_TYPE_LABELS[type as ExerciseType] ??
        TYPE_LABELS[type as ExerciseType] ??
        type
    );
}

/**
 * The first authored sentence of the exercise, whichever field carries it for this type. An
 * unrecognized type renders no preview rather than a guess — the same convention the learner side
 * uses for an unknown assignment rule.
 */
const PREVIEW_FIELDS_BY_TYPE: Partial<Record<ExerciseType, readonly string[]>> = {
    choose_option: ["situation"],
    fill_blank: ["before", "after"],
    reorder: ["instruction"],
    match_pairs: ["instruction"],
    categorize: ["instruction"],
    spot_mistake: ["instruction"],
    rewrite: ["original", "instruction"],
    ai_dialogue: ["scenario"],
    evaluate_call: ["instruction"],
    free_text: ["instruction", "prompt"],
    theory_card: ["title", "heading"],
};

const PREVIEW_LENGTH_LIMIT = 80;

export function summarizeExerciseContent(type: string, content: unknown): string {
    const fields = PREVIEW_FIELDS_BY_TYPE[type as ExerciseType];
    if (!fields || typeof content !== "object" || content === null) return "";

    const record = content as Record<string, unknown>;

    for (const field of fields) {
        const value = record[field];
        if (typeof value === "string" && value.trim().length > 0) {
            const trimmed = value.trim();
            return trimmed.length > PREVIEW_LENGTH_LIMIT
                ? `${trimmed.slice(0, PREVIEW_LENGTH_LIMIT)}…`
                : trimmed;
        }
    }

    return "";
}

/** Renumbers a reordered list so `orderInLesson` stays 1..n with no holes. */
export function moveExerciseInList<TExercise extends { orderInLesson: number }>(
    exercises: readonly TExercise[],
    fromIndex: number,
    toIndex: number
): TExercise[] {
    if (
        fromIndex === toIndex ||
        fromIndex < 0 ||
        toIndex < 0 ||
        fromIndex >= exercises.length ||
        toIndex >= exercises.length
    ) {
        return exercises.map((exercise, index) => ({ ...exercise, orderInLesson: index + 1 }));
    }

    const reordered = [...exercises];
    const [moved] = reordered.splice(fromIndex, 1);
    reordered.splice(toIndex, 0, moved);

    return reordered.map((exercise, index) => ({ ...exercise, orderInLesson: index + 1 }));
}
