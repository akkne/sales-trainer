import type { ChipTone } from "@/shared/components/chip";
import type { AssignmentReminderScope } from "@/features/org-assignments/types/assignment";

/**
 * Every backend value this feature renders, translated once (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4).
 *
 * The dictionary is a module constant rather than a string in markup because the same
 * `failed_threshold` appears on three screens of this slice, and a literal per screen is how it
 * becomes «не дотянул» on one of them.
 *
 * Every lookup falls back to the raw server value: a status this client has never heard of is shown
 * verbatim rather than as a blank or as a guess.
 */
const ASSIGNMENT_STATUS_LABELS: Record<string, string> = {
    draft: "Черновик",
    active: "Выдано",
    closed: "Закрыто",
};

const ASSIGNMENT_STATUS_TONES: Record<string, ChipTone> = {
    draft: "ghost",
    active: "good",
    closed: "neutral",
};

const ASSIGNMENT_SOURCE_TYPE_LABELS: Record<string, string> = {
    training: "По тренингу",
    manual: "Вручную",
    gap_detected: "По провалу на дашборде",
};

const ASSIGNMENT_PROGRESS_STATUS_LABELS: Record<string, string> = {
    not_started: "Не начал",
    in_progress: "В работе",
    failed_threshold: "Ниже порога",
    completed: "Выполнил",
};

const ASSIGNMENT_PROGRESS_STATUS_TONES: Record<string, ChipTone> = {
    not_started: "neutral",
    in_progress: "warn",
    failed_threshold: "bad",
    completed: "good",
};

export function describeAssignmentStatus(status: string): string {
    return ASSIGNMENT_STATUS_LABELS[status] ?? status;
}

export function assignmentStatusTone(status: string): ChipTone {
    return ASSIGNMENT_STATUS_TONES[status] ?? "neutral";
}

export function describeAssignmentSourceType(sourceType: string): string {
    return ASSIGNMENT_SOURCE_TYPE_LABELS[sourceType] ?? sourceType;
}

export function describeProgressStatus(status: string): string {
    return ASSIGNMENT_PROGRESS_STATUS_LABELS[status] ?? status;
}

export function progressStatusTone(status: string): ChipTone {
    return ASSIGNMENT_PROGRESS_STATUS_TONES[status] ?? "neutral";
}

export const CONTENT_KIND_LABELS: Record<string, string> = {
    lesson_version: "Упражнения",
    dialog_scenario: "Разговор",
    reference_material: "Теория",
};

export function describeContentKind(kind: string): string {
    return CONTENT_KIND_LABELS[kind] ?? kind;
}

export const REMINDER_SCOPE_LABELS: Record<AssignmentReminderScope, string> = {
    not_started: "тем, кто не начал",
    unfinished: "всем, кто не закончил",
};

/** The persona difficulty vocabulary ai-service renders; anything else reads as the middle. */
export const PERSONA_DIFFICULTY_OPTIONS: { value: string; label: string }[] = [
    { value: "Easy", label: "Простая" },
    { value: "", label: "Средняя" },
    { value: "Hard", label: "Сложная" },
];
