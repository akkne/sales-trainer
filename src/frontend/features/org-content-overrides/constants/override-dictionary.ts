/**
 * Every backend value O14/O15/O19 shows a human is translated exactly once, here
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §1.4). A literal in a component is how `is_stale` becomes
 * «оригинал обновился» on one screen and «база уехала» on the next.
 */

import type { ChipTone } from "@/shared/components/chip";
import type { OverrideKind } from "../types/content-override";
import type { LessonVersionStatus } from "../types/lesson-editor";

/** §1.4's fixed table, verbatim. */
export const OVERRIDE_KIND_LABELS: Record<OverrideKind, string> = {
    lessons: "урок",
    techniques: "техника",
    "reference-materials": "справка",
    modes: "режим диалога",
};

/** Ordering of the merged table when two rows are equally stale: lessons first, prompts last. */
export const OVERRIDE_KIND_ORDER: Record<OverrideKind, number> = {
    lessons: 0,
    techniques: 1,
    "reference-materials": 2,
    modes: 3,
};

export function describeOverrideKind(kind: string): string {
    return OVERRIDE_KIND_LABELS[kind as OverrideKind] ?? kind;
}

/**
 * The four answers to «в каком отношении моя копия к оригиналу».
 *
 * Staleness arrives from the server as one boolean, but the boolean alone cannot be shown: an
 * override that is stale because the base moved and one that is stale because nobody recorded where
 * it was forked from need different sentences and lead to different reading. 40.15 left the second
 * state expressible on purpose, so the panel names it instead of rounding it to the first.
 */
export type OverrideState = "in_sync" | "base_moved" | "base_unknown" | "base_unpublished";

export interface OverrideStateCopy {
    /** The cell in the list. */
    label: string;
    /** The line under the title on the review screen. */
    hint: string;
    tone: ChipTone;
    /** Whether this row belongs to «есть работа»: the stale queue and the sidebar dot. */
    needsReview: boolean;
}

export const OVERRIDE_STATE_COPY: Record<OverrideState, OverrideStateCopy> = {
    in_sync: {
        label: "совпадает с базой",
        hint: "Оригинал не менялся с тех пор, как вы сделали свою копию.",
        tone: "neutral",
        needsReview: false,
    },
    base_moved: {
        label: "оригинал обновился",
        hint: "Sellevate опубликовала новую версию оригинала. Автоматически с вашей копией она не сливается — решение за вами.",
        tone: "warn",
        needsReview: true,
    },
    base_unknown: {
        label: "основа неизвестна",
        hint: "Мы не знаем, от какой версии оригинала сделана эта копия. Посмотрите оба текста и решите, что оставить.",
        tone: "bad",
        needsReview: true,
    },
    base_unpublished: {
        label: "у оригинала нет версий",
        hint: "У оригинала пока нет ни одной опубликованной версии, поэтому отстать от него ваша копия не может.",
        tone: "ghost",
        needsReview: false,
    },
};

export interface OverrideStateInput {
    isStale: boolean;
    forkedFrom: string | null;
    baseCurrent: string | null;
}

/**
 * Reads the server's own three fields. Nothing here compares content — the client computes no diff
 * and no staleness of its own (docs/TENANCY/ADMIN_UI_DESIGN.md §7).
 */
export function resolveOverrideState({ isStale, forkedFrom, baseCurrent }: OverrideStateInput): OverrideState {
    if (isStale) {
        return forkedFrom === null ? "base_unknown" : "base_moved";
    }

    return baseCurrent === null ? "base_unpublished" : "in_sync";
}

export function describeOverrideState(state: OverrideState): OverrideStateCopy {
    return OVERRIDE_STATE_COPY[state];
}

/** `LessonVersionStatuses`, for the line under the lesson title in O19. */
export const LESSON_VERSION_STATUS_LABELS: Record<LessonVersionStatus, string> = {
    draft: "черновик",
    published: "опубликована",
    archived: "в архиве",
};

export function describeLessonVersionStatus(status: string): string {
    return LESSON_VERSION_STATUS_LABELS[status as LessonVersionStatus] ?? status;
}

/**
 * The publish modal's one mandatory choice. It has no default: a typo fix and a changed correct
 * answer look identical to a diff, so `isBreaking` cannot be inferred and must be answered
 * (docs/TENANCY/CONTENT_MODEL.md §2.4).
 */
export interface PublishScopeOption {
    isBreaking: boolean;
    label: string;
    description: string;
}

export const PUBLISH_SCOPE_OPTIONS: readonly PublishScopeOption[] = [
    {
        isBreaking: false,
        label: "Косметика — опечатка, формулировка, порядок слов",
        description: "График точности продолжится одной линией.",
    },
    {
        isBreaking: true,
        label: "По смыслу — изменился правильный ответ или критерии оценки",
        description:
            "График точности разорвётся: это уже другой вопрос, и сравнивать ответы «до» и «после» нельзя.",
    },
];

/** Shown when `createdNewVersion` came back false. The version number must not move. */
export const NOTHING_TO_PUBLISH_MESSAGE = "Изменений нет — публиковать нечего.";

/** The sticky banner over a lesson whose newest version is still a draft. */
export const UNPUBLISHED_DRAFT_TITLE = "Есть неопубликованные правки";

export function describeUnpublishedDraft(publishedVersionNumber: number | null): string {
    const answeringLine =
        publishedVersionNumber === null
            ? "Команда пока не видит этот урок: опубликованной версии у него ещё нет."
            : `Команда пока отвечает на версию ${publishedVersionNumber}, и их ответы записываются к ней.`;

    return `${answeringLine} Пока вы не опубликуете, ваши правки не попадут ни в статистику, ни в задания.`;
}

/** The in-app confirmation shown when leaving O19 with a live draft. */
export const LEAVE_WITH_DRAFT_TITLE = "Правки сохранены, но команда их не видит";
export const LEAVE_WITH_DRAFT_BODY =
    "У урока есть неопубликованный черновик. Опубликуйте его сейчас — или уйдите, и правки останутся ждать вас здесь.";

/** Copy that has to say the same thing on the list and on the review screen. */
export const NO_AUTO_MERGE_NOTICE =
    "Мы не сливаем эти тексты автоматически. Урок — это проза и критерии оценки; трёхсторонний merge даёт правдоподобную бессмыслицу, по которой потом оценивают живого продавца.";

export const NO_BASE_AT_FORK_NOTICE =
    "Каким оригинал был в момент копирования, мы не знаем — у этого типа материалов нет истории версий.";

/** §6.3: techniques and reference materials are still edited in the platform panel. */
export const PLATFORM_PANEL_EDIT_NOTICE =
    "Редактирование техник и справок пока делается через платформенную панель.";

export const PLATFORM_PANEL_EDIT_HREFS: Partial<Record<OverrideKind, string>> = {
    techniques: "/admin/techniques",
    "reference-materials": "/admin/reference",
};
