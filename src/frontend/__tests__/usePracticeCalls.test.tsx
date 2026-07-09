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
import { useCompanyPracticeCalls, useRecentGoals } from "@/features/companies/hooks/use-practice-calls";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    return {
        wrapper: ({ children }: { children: ReactNode }) => (
            <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
        ),
    };
}

describe("useCompanyPracticeCalls", () => {
    beforeEach(() => mockGet.mockReset());

    it("fetches practice calls from /companies/{id}/practice-calls", async () => {
        mockGet.mockResolvedValue([{ id: "p1", companyId: "c1", dialogSessionId: "d1", goal: "Цель", createdAt: "" }]);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyPracticeCalls("c1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/c1/practice-calls");
        expect(result.current.data).toHaveLength(1);
    });

    it("is disabled when no companyId is given", () => {
        const { wrapper } = createWrapper();
        const { result } = renderHook(() => useCompanyPracticeCalls(null), { wrapper });
        expect(result.current.fetchStatus).toBe("idle");
        expect(mockGet).not.toHaveBeenCalled();
    });
});

describe("useRecentGoals", () => {
    beforeEach(() => mockGet.mockReset());

    it("fetches recent goals from /companies/{id}/recent-goals", async () => {
        mockGet.mockResolvedValue(["Договориться о встрече"]);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useRecentGoals("c1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/c1/recent-goals");
        expect(result.current.data).toEqual(["Договориться о встрече"]);
    });
});
