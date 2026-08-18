import { ApiError } from "@/shared/api/api-client";

export type AssignmentWriteAction = "issue" | "save" | "remind" | "close";

const SERVICE_UNAVAILABLE_STATUS = 503;
const CONFLICT_STATUS = 409;
const BAD_REQUEST_STATUS = 400;

const SERVICE_UNAVAILABLE_MESSAGES: Record<AssignmentWriteAction, string> = {
    issue: "Не удалось проверить состав команды — сервис пользователей не ответил. Задание сохранено черновиком, нажмите «Выдать» ещё раз.",
    save: "Не удалось проверить состав команды — сервис пользователей не ответил. Изменения не сохранены, попробуйте ещё раз.",
    remind: "Не удалось проверить состав команды. Никто не получил напоминание — попробуйте ещё раз.",
    close: "Сервис не ответил. Попробуйте ещё раз.",
};

function readServerMessage(error: ApiError): string | null {
    const message = error.payload.message;

    return typeof message === "string" && message.length > 0 ? message : null;
}

/**
 * What to print when a write fails.
 *
 * 503 is the one that has to be worded exactly: on `activate`, `remind` and `PUT` it means the
 * roster could not be read and therefore **nothing was written** — so the honest instruction is
 * «нажмите ещё раз», not «что-то сломалось». 409 and 400 carry a server sentence that names the
 * actual field or rule, and repeating it beats paraphrasing it.
 */
export function describeAssignmentWriteFailure(
    error: unknown,
    action: AssignmentWriteAction
): string {
    if (!(error instanceof ApiError)) {
        return "Не удалось выполнить действие. Проверьте подключение и попробуйте снова.";
    }

    if (error.status === SERVICE_UNAVAILABLE_STATUS) {
        return SERVICE_UNAVAILABLE_MESSAGES[action];
    }

    if (error.status === CONFLICT_STATUS || error.status === BAD_REQUEST_STATUS) {
        return readServerMessage(error) ?? "Запрос отклонён сервером.";
    }

    if (error.status === 404) {
        return "Задание не найдено. Возможно, черновик удалили.";
    }

    return readServerMessage(error) ?? "Не удалось выполнить действие.";
}

export function isConflictFailure(error: unknown): boolean {
    return error instanceof ApiError && error.status === CONFLICT_STATUS;
}

export function isNotFoundFailure(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404;
}
