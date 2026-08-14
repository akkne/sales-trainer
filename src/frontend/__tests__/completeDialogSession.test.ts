import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { apiClient, RequestTimeoutError } from "@/shared/api/api-client";
import { completeDialogSession } from "@/features/dialog/hooks/use-dialog";
import { describeCompletionError } from "@/features/dialog/services/completion-error-message";

const FEEDBACK = {
    summary: "Хорошо",
    content: "Разбор",
    score: 80,
    generatedAt: "2026-08-14T00:00:00Z",
    xpEarned: 10,
};

function jsonResponse(status: number, body: unknown) {
    return {
        ok: status >= 200 && status < 300,
        status,
        json: async () => body,
    } as unknown as Response;
}

describe("completeDialogSession", () => {
    beforeEach(() => {
        vi.unstubAllGlobals();
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
    });

    it("returns the feedback the backend generated", async () => {
        vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(200, FEEDBACK)));

        await expect(completeDialogSession("sess-1")).resolves.toMatchObject({ score: 80 });
    });

    it("returns null for an empty call (204)", async () => {
        vi.stubGlobal("fetch", vi.fn(async () => jsonResponse(204, undefined)));

        await expect(completeDialogSession("sess-1")).resolves.toBeNull();
    });

    it("falls back to the stored feedback when the session was already completed", async () => {
        // Happens on a retry after the first attempt timed out client-side while the backend
        // finished it — without the fallback the user would be stuck with "session is not active".
        const fetchMock = vi
            .fn()
            .mockResolvedValueOnce(jsonResponse(400, { message: "Session sess-1 is not active" }))
            .mockResolvedValueOnce(jsonResponse(200, { status: "completed", feedback: FEEDBACK }));
        vi.stubGlobal("fetch", fetchMock);

        await expect(completeDialogSession("sess-1")).resolves.toMatchObject({ summary: "Хорошо" });
        expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it("returns null when the already-finished session was abandoned", async () => {
        vi.stubGlobal(
            "fetch",
            vi
                .fn()
                .mockResolvedValueOnce(jsonResponse(400, { message: "Session sess-1 is not active" }))
                .mockResolvedValueOnce(jsonResponse(200, { status: "abandoned", feedback: null }))
        );

        await expect(completeDialogSession("sess-1")).resolves.toBeNull();
    });

    it("gives up instead of waiting forever when the request never answers", async () => {
        // Regression: without a cap, a request that never settles left the call screen on
        // «Готовим разбор…» indefinitely.
        vi.useFakeTimers();
        vi.stubGlobal(
            "fetch",
            vi.fn(
                (_url: string, requestOptions: RequestInit) =>
                    new Promise((_resolve, reject) => {
                        requestOptions.signal?.addEventListener("abort", () => {
                            const abortError = new Error("Aborted");
                            abortError.name = "AbortError";
                            reject(abortError);
                        });
                    })
            )
        );

        const pendingRequest = apiClient.post("/dialog/sessions/sess-1/complete", {}, { timeoutMs: 1_000 });
        const assertion = expect(pendingRequest).rejects.toBeInstanceOf(RequestTimeoutError);
        await vi.advanceTimersByTimeAsync(1_000);
        await assertion;
    });
});

describe("describeCompletionError", () => {
    it("replaces the transport-level timeout message with a readable one", () => {
        expect(describeCompletionError(new RequestTimeoutError(120_000))).toBe(
            "Разбор не успел подготовиться. Попробуйте ещё раз"
        );
    });

    it("keeps a backend error message as-is", () => {
        expect(describeCompletionError(new Error("AI service is temporarily unavailable"))).toBe(
            "AI service is temporarily unavailable"
        );
    });
});
