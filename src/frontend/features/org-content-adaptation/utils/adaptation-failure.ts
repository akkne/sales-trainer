import { ApiError } from "@/shared/api/api-client";

/**
 * What to print when the batch routes refuse something.
 *
 * <b>The oversized-stage refusal is advice, not an error</b> (docs/TENANCY/ADMIN_UI_DESIGN.md §2
 * O12): a stage with four hundred exercises is four hundred model calls and a queue nobody will
 * ever walk, so the honest answer names the number and tells the РОП to pick a narrower stage.
 *
 * <b>The refusals arrive in English.</b> `ContentAdaptationValidationException` and
 * `ContentAdaptationStateException` carry developer prose — «Stage 'closing' holds 412 exercises,
 * which is above the per-batch ceiling of 60…» — and the organization panel is Russian. There is no
 * machine-readable refusal payload on these two routes the way there is on
 * `POST /admin/content-generation/{id}/approve` (which carries an `insufficiency` object), so the
 * count is recovered from the sentence and the sentence itself is never shown. A message shape this
 * client does not recognise degrades to a Russian sentence about the stage, never to English.
 */

const BAD_REQUEST_STATUS = 400;
const CONFLICT_STATUS = 409;
const NOT_FOUND_STATUS = 404;

/** `ContentAdaptationJobService.StartAsync`: "Stage 'x' holds 412 exercises, which is above the per-batch ceiling of 60." */
const OVERSIZED_STAGE_PATTERN = /holds (\d+) exercises/i;

/** `ContentAdaptationJobService.StartAsync`: "Stage 'x' has no exercises to adapt." */
const EMPTY_STAGE_PATTERN = /has no exercises to adapt/i;

export interface AdaptationStartFailure {
    /** What the person reads. Always Russian. */
    message: string;
    /** A batch over this stage is already open — the screen offers a link to it instead of a retry. */
    isLiveBatchConflict: boolean;
}

function readServerMessage(error: ApiError): string {
    const message = error.payload.message;

    return typeof message === "string" ? message : "";
}

/**
 * The refusal a person gets after pressing «Переписать этап». Two of the four cases are the design's
 * own words; the rest stay generic rather than paraphrasing a sentence written for a developer.
 */
export function describeStartFailure(error: unknown): AdaptationStartFailure {
    if (!(error instanceof ApiError)) {
        return {
            message: "Не удалось создать пакет. Проверьте подключение и попробуйте снова.",
            isLiveBatchConflict: false,
        };
    }

    const serverMessage = readServerMessage(error);

    if (error.status === CONFLICT_STATUS) {
        return {
            message: "По этому этапу уже идёт пакет. Закончите разбирать его, прежде чем запускать второй.",
            isLiveBatchConflict: true,
        };
    }

    if (error.status === BAD_REQUEST_STATUS) {
        const oversizedStageMatch = OVERSIZED_STAGE_PATTERN.exec(serverMessage);
        if (oversizedStageMatch) {
            return {
                message:
                    `В этапе ${oversizedStageMatch[1]} упражнений — это дорого и это очередь, ` +
                    "которую никто не разберёт. Выберите этап поуже.",
                isLiveBatchConflict: false,
            };
        }

        if (EMPTY_STAGE_PATTERN.test(serverMessage)) {
            return {
                message: "В этом этапе нет упражнений — переписывать нечего.",
                isLiveBatchConflict: false,
            };
        }

        return { message: "Этап выбран неверно — запрос отклонён.", isLiveBatchConflict: false };
    }

    return {
        message: "Не удалось создать пакет. Попробуйте ещё раз.",
        isLiveBatchConflict: false,
    };
}

/**
 * The refusal a person gets on «Принять» / «Отклонить» / «Повторить».
 *
 * 409 on accept means one of two things and both end the same way — the proposal cannot be applied
 * as it stands, and a re-run is the answer. Merging is what 40.18 refused to build and what nothing
 * here will build either.
 */
export function describeItemActionFailure(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "Не удалось отправить ответ. Проверьте подключение и попробуйте снова.";
    }

    if (error.status === CONFLICT_STATUS) {
        return (
            "Сервер отказался применять предложение: упражнение изменилось после того, как " +
            "предложение было посчитано, либо применять нечего. Запустите пакет заново."
        );
    }

    if (error.status === NOT_FOUND_STATUS) {
        return "Предложение не найдено — возможно, пакет удалили.";
    }

    return "Не удалось отправить ответ. Попробуйте ещё раз.";
}

export function isNotFoundFailure(error: unknown): boolean {
    return error instanceof ApiError && error.status === NOT_FOUND_STATUS;
}
