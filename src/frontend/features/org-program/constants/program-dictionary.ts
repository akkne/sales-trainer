import type { ChipTone } from "@/shared/components/chip";

/**
 * O18 «Программа обучения» — every visible string of the screen in one place (design §1.4: a status
 * word translated at the point of use becomes two different words on two screens).
 */

export const PROGRAM_PAGE_TITLE = "Программа обучения";

export const PROGRAM_PAGE_SUBTITLE =
    "Пока программа не опубликована, команда учится по живому дереву навыков и видит все изменения " +
    "сразу. Опубликованная версия фиксирует порядок и версии уроков для тех, кого вы на неё зачислите.";

/**
 * The paragraph the design calls «часть защиты»: it is on the screen so that the absence of a
 * «перевести всех» button reads as a decision rather than as a missing feature
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §7, docs/DONT_FORGET.md → блок 40.17).
 */
export const NO_BULK_SWITCH_EXPLANATION =
    "Перевести человека на новую версию может только он сам — из своего приложения, увидев, что " +
    "изменилось. Кнопки «перевести всех» нет намеренно: иначе гарантия «программу под учащимся никто " +
    "не переставит» перестаёт быть свойством системы.";

/**
 * Stated because it is true today and because the screen would otherwise imply the opposite:
 * `/skill-tree`, `/lessons` and `/exercises/*` still read the live tree, and only `GET /program`
 * serves the pin (docs/DONT_FORGET.md → блок 40.17). Publishing changes what this screen guarantees,
 * not yet what a manager sees in the application.
 */
export const PIN_NOT_YET_ON_LEARNER_SCREENS_NOTE =
    "Пока приложение продавца показывает живое дерево навыков: зачисление фиксирует программу в " +
    "системе, но экраны обучения ещё читают её напрямую. Порядок, зафиксированный здесь, вступит в " +
    "силу для учащегося, когда обучение начнёт читать программу.";

export const PROGRAM_VERSION_STATUS_LABELS: Record<string, string> = {
    draft: "Черновик",
    published: "Опубликована",
    archived: "В архиве",
};

export const PROGRAM_VERSION_STATUS_TONES: Record<string, ChipTone> = {
    draft: "warn",
    published: "good",
    archived: "neutral",
};

/** An unknown status is shown raw rather than guessed at — the same rule the panel uses everywhere. */
export function describeProgramVersionStatus(status: string): string {
    return PROGRAM_VERSION_STATUS_LABELS[status] ?? status;
}

export function resolveProgramVersionStatusTone(status: string): ChipTone {
    return PROGRAM_VERSION_STATUS_TONES[status] ?? "neutral";
}

export const NO_VERSIONS_TITLE = "Программа ещё не опубликована";

export const NO_VERSIONS_DESCRIPTION =
    "Сейчас команда учится по живому дереву навыков: любое изменение видно всем сразу, включая тех, " +
    "кто уже на середине. Соберите черновик из дерева, посмотрите его и опубликуйте — тогда порядок " +
    "и версии уроков можно будет зафиксировать за конкретными людьми.";

export const NO_ENROLLMENTS_TITLE = "Никто не зачислен";

export const NO_ENROLLMENTS_DESCRIPTION =
    "Опубликованная версия ни на кого не влияет, пока вы не зачислите людей. Незачисленный человек " +
    "продолжает учиться по живому дереву.";

export const ENROLL_HINT = "Зачислит новичков и не тронет тех, кто уже учится.";

export const ENROLL_BUTTON_LABEL = "Зачислить ещё";

export const REBUILD_DRAFT_BUTTON_LABEL = "Пересобрать черновик из дерева";

export const PUBLISH_BUTTON_LABEL = "Опубликовать";

export const VIEW_VERSION_BUTTON_LABEL = "Посмотреть";

export const DIFF_BUTTON_LABEL = "Что изменилось";

export const PUBLISH_CONFIRM_TITLE = "Опубликовать черновик?";

export const PUBLISH_CONFIRM_BODY =
    "Опубликованная версия замораживается навсегда: ни порядок, ни версии уроков в ней больше не " +
    "меняются. Никого из тех, кто уже учится, публикация не двигает — новую версию получат только " +
    "те, кого вы зачислите после неё, и те, кто перейдёт сам.";

export const PUBLISH_NO_CHANGES_MESSAGE =
    "Изменений нет, новая версия не создана.";

export const PUBLISH_NO_DRAFT_MESSAGE =
    "Черновика нет. Соберите его из дерева навыков и попробуйте снова.";

export const PUBLISH_FAILED_MESSAGE = "Не удалось опубликовать. Попробуйте ещё раз.";

export const DRAFT_FAILED_MESSAGE = "Не удалось собрать черновик. Попробуйте ещё раз.";

export const ENROLL_FAILED_MESSAGE = "Не удалось зачислить. Попробуйте ещё раз.";

export const ENROLL_NO_PUBLISHED_VERSION_MESSAGE =
    "Зачислять пока некуда: опубликованной версии программы нет.";

export const NO_ORGANIZATION_MESSAGE =
    "Программа принадлежит одной организации. Войдите в организацию из реестра, чтобы её собрать.";

export const LOAD_FAILED_TITLE = "Не удалось загрузить программу";

export const LOAD_FAILED_MESSAGE = "Проверьте подключение и попробуйте снова.";

export const DIFF_FAILED_TITLE = "Не удалось загрузить изменения";

export const UNKNOWN_LESSON_TITLE = "Урок недоступен";

export const DIFF_BREAKING_WARNING =
    "В некоторых уроках изменился правильный ответ или критерии оценки.";

export const DIFF_EMPTY_MESSAGE =
    "Между этими версиями нет ни одного отличия — ни добавленных, ни убранных, ни переставленных уроков.";

export const DIFF_BUCKET_LABELS = {
    added: "Добавлены",
    removed: "Убраны",
    changed: "Новая версия урока",
    moved: "Переставлены",
} as const;

export const ENROLLMENT_TABLE_TITLE = "Зачисления";

export const BEHIND_CHIP_LABEL = "Отстаёт";

export const CURRENT_CHIP_LABEL = "Последняя";

export const SWITCHED_HIMSELF_LABEL = "перешёл сам";

export const WHAT_CHANGES_FOR_PERSON_LABEL = "Что изменится у него";

/**
 * Shown only when the roster request itself failed. The count of unenrolled people is then unknown,
 * and «0 без зачисления» would be a false statement rather than a missing one.
 */
export const ROSTER_UNAVAILABLE_NOTE =
    "Список сотрудников сейчас не загрузился, поэтому не видно, кто ещё не зачислен. Имена в таблице " +
    "показаны по идентификаторам.";
