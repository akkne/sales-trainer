import { RequestTimeoutError } from "@/shared/api/api-client";

/**
 * User-facing reason why the call analysis is missing. A timeout carries a technical English
 * message from the transport layer, which must never reach the call screen.
 */
export function describeCompletionError(error: unknown): string {
    if (error instanceof RequestTimeoutError) {
        return "Разбор не успел подготовиться. Попробуйте ещё раз";
    }
    return error instanceof Error ? error.message : "Не удалось подготовить разбор";
}
