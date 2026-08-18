import { ApiError } from "@/shared/api/api-client";
import {
    MATERIAL_MAXIMUM_LENGTH,
    describeSufficiencyGapMessage,
} from "@/features/org-content-generation/constants/generation-dictionary";
import type {
    ContentInsufficiency,
    ContentSufficiencyGap,
} from "@/features/org-content-generation/types/content-generation";

const BAD_REQUEST_STATUS = 400;
const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;

export type ContentGenerationWriteAction =
    | "start"
    | "saveStructure"
    | "supplementMaterial"
    | "approve"
    | "retry"
    | "unarchiveLesson";

const FALLBACK_MESSAGES: Record<ContentGenerationWriteAction, string> = {
    start: "Не удалось начать прогон. Проверьте подключение и попробуйте снова.",
    saveStructure: "Не удалось сохранить структуру. Проверьте подключение — правки остались на экране.",
    supplementMaterial: "Не удалось добавить материал. Проверьте подключение и попробуйте снова.",
    approve: "Не удалось запустить генерацию. Проверьте подключение и попробуйте снова.",
    retry: "Не удалось повторить прогон. Проверьте подключение и попробуйте снова.",
    unarchiveLesson: "Не удалось показать урок команде. Проверьте подключение и попробуйте снова.",
};

/**
 * The 409 that is **not** a refusal: «прогон уже ушёл дальше». It happens for one ordinary reason —
 * a second tab, or the polling screen, moved the run while this one was still looking at the old
 * state — so the instruction is to re-read rather than to retry.
 */
const STALE_STATE_MESSAGES: Record<ContentGenerationWriteAction, string> = {
    start: "Запрос отклонён сервером.",
    saveStructure: "Прогон уже ушёл дальше — структуру на этом шаге изменить нельзя. Обновите страницу.",
    supplementMaterial: "Прогон уже ушёл дальше — добавлять материал можно только пока он ждёт материала.",
    approve: "Прогон уже ушёл дальше. Обновите страницу, чтобы увидеть, где он сейчас.",
    retry: "Повторить можно только прогон, который завершился ошибкой.",
    unarchiveLesson: "Запрос отклонён сервером.",
};

function readServerMessage(error: ApiError): string | null {
    const message = error.payload.message;
    return typeof message === "string" && message.length > 0 ? message : null;
}

function isSufficiencyGap(candidate: unknown): candidate is ContentSufficiencyGap {
    if (typeof candidate !== "object" || candidate === null) return false;
    const gap = candidate as Record<string, unknown>;
    return typeof gap.code === "string" && typeof gap.message === "string";
}

/**
 * `POST …/approve` answers **409 with the gap list in the body** when the structure is still too
 * thin — the server has already moved the run to `insufficient` before answering, so the polling
 * screen and the caller who pressed the button see the same thing. Reading the body lets the screen
 * show the refusal without waiting a round trip for the re-read it also issues.
 *
 * A 409 without an `insufficiency` is the other kind: see `STALE_STATE_MESSAGES`.
 */
export function readInsufficiencyFromConflict(error: unknown): ContentInsufficiency | null {
    if (!(error instanceof ApiError) || error.status !== CONFLICT_STATUS) return null;

    const insufficiency = error.payload.insufficiency;
    if (typeof insufficiency !== "object" || insufficiency === null) return null;

    const candidate = insufficiency as Record<string, unknown>;
    if (!Array.isArray(candidate.gaps)) return null;

    const gaps = candidate.gaps.filter(isSufficiencyGap);
    if (gaps.length === 0) return null;

    return {
        stage: typeof candidate.stage === "string" ? candidate.stage : "",
        gaps,
        note: typeof candidate.note === "string" ? candidate.note : null,
    };
}

/**
 * What to print when a write fails. A refusal never reaches here — it is a state with its own
 * screen, and the caller pulls it out with `readInsufficiencyFromConflict` first.
 */
export function describeContentGenerationFailure(
    error: unknown,
    action: ContentGenerationWriteAction
): string {
    if (!(error instanceof ApiError)) return FALLBACK_MESSAGES[action];

    if (error.status === NOT_FOUND_STATUS) {
        return action === "unarchiveLesson"
            ? "Урок не найден. Возможно, его удалили."
            : "Прогон не найден. Возможно, его удалили.";
    }

    if (error.status === CONFLICT_STATUS) {
        return STALE_STATE_MESSAGES[action];
    }

    if (error.status === BAD_REQUEST_STATUS) {
        return readServerMessage(error) ?? FALLBACK_MESSAGES[action];
    }

    return readServerMessage(error) ?? FALLBACK_MESSAGES[action];
}

export function isNotFoundFailure(error: unknown): boolean {
    return error instanceof ApiError && error.status === NOT_FOUND_STATUS;
}

/** The gaps of a refusal, minus the ones nothing can be printed for. */
export function readableGapMessages(insufficiency: ContentInsufficiency | null): string[] {
    if (!insufficiency) return [];

    return insufficiency.gaps
        .map((gap) => describeSufficiencyGapMessage(gap.code, gap.message))
        .filter((message): message is string => message !== null);
}

/**
 * The start form's only two client-side rules, and they are the server's own
 * (`ContentGenerationJobService`): an empty textarea and a ceiling. **Thin material is not one of
 * them** — refusing it here would replace an answerable run with a form error and would hide the
 * sentence saying what to bring.
 */
export function validateStartMaterial(material: string): string | null {
    if (material.trim().length === 0) {
        return "Вставьте материал: презентацию продукта, скрипт звонка или расшифровку разговора.";
    }

    if (material.length > MATERIAL_MAXIMUM_LENGTH) {
        return `Материал длиннее ${MATERIAL_MAXIMUM_LENGTH.toLocaleString("ru-RU")} символов. Разбейте его на несколько прогонов.`;
    }

    return null;
}

export function validateStartTitle(title: string): string | null {
    if (title.trim().length === 0) {
        return "Назовите прогон — по этому названию вы найдёте урок позже.";
    }

    return null;
}
