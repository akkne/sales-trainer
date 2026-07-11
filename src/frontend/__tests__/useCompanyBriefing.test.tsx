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
import {
    useCompanyBriefing,
    useGenerateCompanyBriefing,
} from "@/features/companies/hooks/use-company-briefing";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return {
        queryClient,
        wrapper: ({ children }: { children: ReactNode }) => (
            <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        ),
    };
}

describe("useCompanyBriefing", () => {
    beforeEach(() => {
        mockGet.mockReset();
    });

    it("fetches the cached briefing from /companies/{id}/briefing", async () => {
        mockGet.mockResolvedValue({ content: "## Кто они\n- Тест", generatedAt: "2026-07-10T12:00:00Z" });
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyBriefing("1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/1/briefing");
        expect(result.current.data?.content).toContain("Тест");
    });

    it("normalizes a 204 (never generated) response to an empty briefing", async () => {
        mockGet.mockResolvedValue(undefined);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyBriefing("1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(result.current.data).toEqual({ content: null, generatedAt: null });
    });

    it("does not fetch when companyId is null", () => {
        const { wrapper } = createWrapper();

        renderHook(() => useCompanyBriefing(null), { wrapper });

        expect(mockGet).not.toHaveBeenCalled();
    });
});

describe("useGenerateCompanyBriefing", () => {
    beforeEach(() => {
        mockPost.mockReset();
        toastError.mockReset();
    });

    it("posts to /companies/{id}/briefing and caches the result", async () => {
        mockPost.mockResolvedValue({ content: "## Кто они\n- Новая шпаргалка", generatedAt: "2026-07-10T12:00:00Z" });
        const { queryClient, wrapper } = createWrapper();

        const { result } = renderHook(() => useGenerateCompanyBriefing("1"), { wrapper });
        result.current.mutate();

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/1/briefing", {});
        expect(queryClient.getQueryData(["companies", "1", "briefing"])).toEqual({
            content: "## Кто они\n- Новая шпаргалка",
            generatedAt: "2026-07-10T12:00:00Z",
        });
    });

    it("shows an error toast when generation fails", async () => {
        mockPost.mockRejectedValue(new Error("AI service unavailable"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useGenerateCompanyBriefing("1"), { wrapper });
        result.current.mutate();

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("AI service unavailable"));
    });
});
