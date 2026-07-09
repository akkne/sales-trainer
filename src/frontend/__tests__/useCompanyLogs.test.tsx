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
import {
    useCompanyLogs,
    useAddCallLog,
    useUpdateCallLog,
    useDeleteCallLog,
} from "@/features/companies/hooks/use-company-logs";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;
const mockPut = apiClient.put as ReturnType<typeof vi.fn>;
const mockDelete = apiClient.delete as ReturnType<typeof vi.fn>;

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

const LOG = {
    id: "l1", companyId: "c1", contactName: "Иван", subject: "Тема", outcome: "Итог",
    occurredAt: "2026-07-01T00:00:00Z", createdAt: "", updatedAt: "",
};

describe("useCompanyLogs", () => {
    beforeEach(() => mockGet.mockReset());

    it("fetches logs from /companies/{id}/logs", async () => {
        mockGet.mockResolvedValue([LOG]);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyLogs("c1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/c1/logs");
        expect(result.current.data).toHaveLength(1);
    });
});

describe("useAddCallLog", () => {
    beforeEach(() => {
        mockPost.mockReset();
        mockGet.mockReset();
    });

    it("posts the log and invalidates logs/company/companies queries", async () => {
        mockPost.mockResolvedValue(LOG);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useAddCallLog("c1"), { wrapper });
        result.current.mutate({ contactName: "Иван", subject: "Тема", outcome: "Итог", occurredAt: "2026-07-01T00:00:00Z" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/c1/logs", expect.objectContaining({ contactName: "Иван" }));
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "logs"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
    });
});

describe("useUpdateCallLog", () => {
    beforeEach(() => mockPut.mockReset());

    it("puts to /companies/{id}/logs/{logId} and invalidates the logs list", async () => {
        mockPut.mockResolvedValue(LOG);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useUpdateCallLog("c1"), { wrapper });
        result.current.mutate({ logId: "l1", contactName: "Иван", subject: "Тема", outcome: "Итог", occurredAt: "2026-07-01T00:00:00Z" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPut).toHaveBeenCalledWith("/companies/c1/logs/l1", expect.objectContaining({ contactName: "Иван" }));
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "logs"] });
    });
});

describe("useDeleteCallLog", () => {
    beforeEach(() => mockDelete.mockReset());

    it("deletes the log and invalidates logs/company/companies queries", async () => {
        mockDelete.mockResolvedValue(undefined);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useDeleteCallLog("c1"), { wrapper });
        result.current.mutate("l1");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockDelete).toHaveBeenCalledWith("/companies/c1/logs/l1");
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "logs"] });
    });
});
