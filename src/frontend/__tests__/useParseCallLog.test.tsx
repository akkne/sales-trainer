import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

vi.mock("@/shared/api/api-client", () => ({
    apiClient: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

const toastError = vi.fn();
vi.mock("@/features/notifications/store/toast-store", () => ({
    toast: { error: (...args: unknown[]) => toastError(...args) },
}));

import { apiClient } from "@/shared/api/api-client";
import { useParseCallLog } from "@/features/companies/hooks/use-parse-call-log";

const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
}

describe("useParseCallLog", () => {
    beforeEach(() => {
        mockPost.mockReset();
        toastError.mockReset();
    });

    it("posts the raw text to /companies/{id}/logs/parse and returns the parsed fields", async () => {
        mockPost.mockResolvedValue({
            contactName: "Иван Петров",
            subject: "Обсудили условия",
            outcome: "Взял паузу подумать",
            occurredAt: "2026-07-01T00:00:00Z",
        });

        const { result } = renderHook(() => useParseCallLog("1"), { wrapper: createWrapper() });
        result.current.mutate("сырые заметки о звонке");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/1/logs/parse", { rawText: "сырые заметки о звонке" });
        expect(result.current.data?.contactName).toBe("Иван Петров");
        expect(result.current.data?.occurredAt).toBe("2026-07-01T00:00:00Z");
    });

    it("shows an error toast when parsing fails", async () => {
        mockPost.mockRejectedValue(new Error("AI service unavailable"));

        const { result } = renderHook(() => useParseCallLog("1"), { wrapper: createWrapper() });
        result.current.mutate("сырые заметки");

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("AI service unavailable"));
    });
});
