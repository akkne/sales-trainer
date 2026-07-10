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

import { apiClient } from "@/shared/api/api-client";
import { useCompanyReadiness } from "@/features/companies/hooks/use-company-readiness";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

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

describe("useCompanyReadiness", () => {
    beforeEach(() => {
        mockGet.mockReset();
    });

    it("fetches the readiness score from /companies/{id}/readiness", async () => {
        mockGet.mockResolvedValue({
            score: 72,
            strengths: ["Уверенный тон"],
            gaps: ["Работа с ценой"],
            recommendation: "Потренируйте возражения.",
            generatedAt: "2026-07-10T12:00:00Z",
        });
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyReadiness("1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/1/readiness");
        expect(result.current.data?.score).toBe(72);
        expect(result.current.data?.gaps).toEqual(["Работа с ценой"]);
    });

    it("normalizes a 204 (no data yet) response to an empty readiness", async () => {
        mockGet.mockResolvedValue(undefined);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyReadiness("1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(result.current.data).toEqual({
            score: null,
            strengths: null,
            gaps: null,
            recommendation: null,
            generatedAt: null,
        });
    });

    it("does not fetch when companyId is null", () => {
        const { wrapper } = createWrapper();

        renderHook(() => useCompanyReadiness(null), { wrapper });

        expect(mockGet).not.toHaveBeenCalled();
    });
});
