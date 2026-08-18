import { RequestTimeoutError } from "@/shared/api/api-client";

/**
 * User-facing reason why the call analysis is missing. A timeout carries a technical English
 * message from the transport layer, which must never reach the call screen.
 */
export function describeCompletionError(error: unknown): string {
    if (error instanceof RequestTimeoutError) {
        return "Разбор не успел подготовиться. Попробуйте ещё раз";
    }
    // fetch rejects with a TypeError when the request never got a usable answer — the server is
    // down, the connection dropped, or the response carried no CORS headers (which is what a
    // gateway timeout looks like from the browser). "Failed to fetch" tells the user nothing.
    if (error instanceof TypeError) {
        return "Сервер не ответил. Проверьте соединение и попробуйте ещё раз";
    }
    return error instanceof Error ? error.message : "Не удалось подготовить разбор";
}
