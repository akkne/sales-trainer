"use client";

import { useMutation } from "@tanstack/react-query";
import { ApiError, apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import type { DemoRequestAcceptedResponse, DemoRequestPayload } from "@/features/demo/types";

const GENERIC_FAILURE_MESSAGE = "Не удалось отправить заявку. Попробуйте ещё раз.";

/// Turns a `429` (repeat submission from the same email too soon) into a sentence the visitor can
/// act on instead of a generic failure — everything else collapses to one message, since the
/// backend's validation problem details are not meant to be shown verbatim to a company decision-maker.
export function describeDemoRequestFailure(error: unknown): string {
    if (error instanceof ApiError && error.status === 429) {
        const retryAfterSeconds =
            typeof error.payload.retryAfterSeconds === "number"
                ? error.payload.retryAfterSeconds
                : null;
        const retryAfterMinutes = retryAfterSeconds
            ? Math.max(1, Math.ceil(retryAfterSeconds / 60))
            : null;

        return retryAfterMinutes
            ? `Заявка с этим email уже отправлена. Повторить можно будет примерно через ${retryAfterMinutes} мин.`
            : "Заявка с этим email уже отправлена. Мы уже с вами свяжемся, повторная отправка пока недоступна.";
    }
    return GENERIC_FAILURE_MESSAGE;
}

export function useDemoRequest() {
    return useMutation<DemoRequestAcceptedResponse, unknown, DemoRequestPayload>({
        mutationFn: (payload) =>
            apiClient.post<DemoRequestAcceptedResponse>("/demo-requests", payload),
        onSuccess: (response) => {
            clientLogger.info("Demo request submitted", { demoRequestId: response.id });
        },
        onError: (error) => {
            clientLogger.warn("Demo request submission failed", {
                error: (error as Error).message,
            });
        },
    });
}
