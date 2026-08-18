import type { ChipTone } from "@/shared/components/chip";
import type {
    ContentAdaptationItemStatus,
    ContentAdaptationJobStatus,
    ContentAdaptationMode,
} from "@/features/org-content-adaptation/types/adaptation";

/**
 * Every value the batch adaptation screens (O12, O13) show a person, translated exactly once
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4).
 *
 * Two rules hold everywhere in this file:
 *
 * 1. **A lookup that misses falls back to the raw server value.** A status this build has never
 *    heard of is printed verbatim, never blank and never guessed into the nearest known word.
 * 2. **The review vocabulary is closed at seven codes** (`ContentReviewFindingCodes`). This client
 *    adds a short Russian title per code so a queue can be scanned, and nothing else: the sentence
 *    the РОП reads is `finding.message`, written on the server so that two runs over the same
 *    exercise produce the same complaint. A code outside the seven is shown as the code itself.
 */

/** `ContentAdaptationModes`. */
export const ADAPTATION_MODE_LABELS: Record<ContentAdaptationMode, string> = {
    tone_rewrite: "Переписать под нас",
    quality_review: "Проверить, что написали руками",
};

/** The short form for a table cell, where the tab already said which queue this is. */
export const ADAPTATION_MODE_SHORT_LABELS: Record<ContentAdaptationMode, string> = {
    tone_rewrite: "Правка тона",
    quality_review: "Ревью",
};

export function describeAdaptationMode(mode: string): string {
    return ADAPTATION_MODE_LABELS[mode as ContentAdaptationMode] ?? mode;
}

export function describeAdaptationModeShort(mode: string): string {
    return ADAPTATION_MODE_SHORT_LABELS[mode as ContentAdaptationMode] ?? mode;
}

/** `ContentAdaptationStatuses`. */
const ADAPTATION_JOB_STATUS_LABELS: Record<ContentAdaptationJobStatus, string> = {
    preparing: "Готовим предложения",
    awaiting_review: "Ждёт вашего ответа",
    completed: "Разобрано",
    failed: "Ошибка",
};

const ADAPTATION_JOB_STATUS_TONES: Record<ContentAdaptationJobStatus, ChipTone> = {
    preparing: "neutral",
    awaiting_review: "warn",
    completed: "good",
    failed: "bad",
};

export function describeJobStatus(status: string): string {
    return ADAPTATION_JOB_STATUS_LABELS[status as ContentAdaptationJobStatus] ?? status;
}

export function jobStatusTone(status: string): ChipTone {
    return ADAPTATION_JOB_STATUS_TONES[status as ContentAdaptationJobStatus] ?? "neutral";
}

/** `ContentAdaptationItemStatuses` — the queue's own state machine, as a person reads it. */
const ADAPTATION_ITEM_STATUS_LABELS: Record<ContentAdaptationItemStatus, string> = {
    pending: "в очереди",
    proposed: "ждёт",
    unchanged: "без изменений",
    accepted: "принято",
    rejected: "отклонено",
    failed: "ошибка",
};

const ADAPTATION_ITEM_STATUS_TONES: Record<ContentAdaptationItemStatus, ChipTone> = {
    pending: "ghost",
    proposed: "warn",
    unchanged: "neutral",
    accepted: "good",
    rejected: "neutral",
    failed: "bad",
};

export function describeItemStatus(status: string): string {
    return ADAPTATION_ITEM_STATUS_LABELS[status as ContentAdaptationItemStatus] ?? status;
}

export function itemStatusTone(status: string): ChipTone {
    return ADAPTATION_ITEM_STATUS_TONES[status as ContentAdaptationItemStatus] ?? "neutral";
}

/**
 * The seven codes of `ContentReviewFindingCodes`, as short titles for scanning a queue. The full
 * sentence — including what edit fixes the defect — always comes from the server's `message`.
 */
export const CONTENT_REVIEW_FINDING_TITLES: Record<string, string> = {
    ambiguous_correct_answer: "Правильный ответ неоднозначен",
    multiple_correct_answers: "Верных вариантов несколько",
    obvious_distractors: "Неверные варианты слишком очевидны",
    answer_given_away: "Ответ подсказан в формулировке",
    unmeasurable_criteria: "Критерии нельзя проверить",
    missing_explanation: "Нет объяснения, почему ответ верен",
    banned_claim_rewarded: "Поощряется запрещённое обещание",
};

/** A code outside the seven is printed as itself — the vocabulary is closed, so this is a signal. */
export function describeFindingCode(code: string): string {
    return CONTENT_REVIEW_FINDING_TITLES[code] ?? code;
}

/** `ContentReviewFindingCodes.BlockingSeverity` / `AdvisorySeverity`. */
const FINDING_SEVERITY_LABELS: Record<string, string> = {
    blocking: "Критично",
    advisory: "Совет",
};

const FINDING_SEVERITY_TONES: Record<string, ChipTone> = {
    blocking: "bad",
    advisory: "neutral",
};

export function describeFindingSeverity(severity: string): string {
    return FINDING_SEVERITY_LABELS[severity] ?? severity;
}

export function findingSeverityTone(severity: string): ChipTone {
    return FINDING_SEVERITY_TONES[severity] ?? "neutral";
}

/**
 * `Exercises.Type`. The platform panel names these in English (`features/admin/.../types.ts`,
 * §0.3: that panel is internal); the organization panel is the customer's, so it names them in
 * Russian and keeps the same fallback-to-raw rule.
 */
const EXERCISE_TYPE_LABELS: Record<string, string> = {
    choose_option: "выбор варианта",
    fill_blank: "пропуск",
    reorder: "порядок",
    match_pairs: "пары",
    categorize: "категории",
    spot_mistake: "найти ошибку",
    rewrite: "переписать",
    ai_dialogue: "диалог с ИИ",
    evaluate_call: "разбор звонка",
    free_text: "свободный ответ",
    theory_card: "теория",
};

export function describeExerciseType(exerciseType: string): string {
    return EXERCISE_TYPE_LABELS[exerciseType] ?? exerciseType;
}

/**
 * Printed under «Принять» on every rewrite. Without it a РОП accepts forty edits and then waits for
 * the trainer to show them — accepting writes the draft row, and only publishing a lesson version
 * reaches the team (docs/CONTENT_PIPELINE.md §6a, «No version is published»).
 */
export const ACCEPT_PUBLISHING_CAVEAT =
    "Принятая правка попадёт в упражнение, но команда увидит её только после публикации новой версии урока.";

/** Shown over a disabled «Принять» when the exercise moved after the proposal was computed. */
export const STALE_ITEM_EXPLANATION =
    "Упражнение изменили после того, как предложение было посчитано. Принять нельзя — сервер откажет. Запустите пакет заново.";

/** Review mode has nothing to apply: a finding is a diagnosis, and the server answers 409 to accept. */
export const REVIEW_MODE_REJECT_CAVEAT =
    "Замечание закрывается без изменений. Чтобы исправить упражнение, откройте его в редакторе урока.";
