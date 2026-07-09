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
    useCompanies,
    useCreateCompany,
    useDeleteCompany,
    type CompanySummary,
} from "@/features/companies/hooks/use-companies";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;
const mockDelete = apiClient.delete as ReturnType<typeof vi.fn>;

const COMPANIES: CompanySummary[] = [
    { id: "1", name: "Ромашка", descriptionExcerpt: "", callLogCount: 0, practiceCallCount: 0, createdAt: "", updatedAt: "2026-07-01T00:00:00Z" },
    { id: "2", name: "Вектор", descriptionExcerpt: "", callLogCount: 0, practiceCallCount: 0, createdAt: "", updatedAt: "2026-07-01T00:00:00Z" },
];

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

describe("useCompanies", () => {
    beforeEach(() => {
        mockGet.mockReset();
        mockPost.mockReset();
        mockDelete.mockReset();
    });

    it("fetches the full list from /companies", async () => {
        mockGet.mockResolvedValue(COMPANIES);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanies(), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies");
        expect(result.current.data).toHaveLength(2);
    });

    it("filters the cached list client-side by name (case-insensitive)", async () => {
        mockGet.mockResolvedValue(COMPANIES);
        const { wrapper } = createWrapper();

        const { result, rerender } = renderHook(({ search }) => useCompanies(search), {
            wrapper,
            initialProps: { search: "" },
        });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(result.current.data).toHaveLength(2);

        rerender({ search: "вект" });
        await waitFor(() => expect(result.current.data).toHaveLength(1));
        expect(result.current.data?.[0].name).toBe("Вектор");

        // Only the initial fetch happened — filtering never re-hits the network.
        expect(mockGet).toHaveBeenCalledTimes(1);
    });
});

describe("useCreateCompany", () => {
    beforeEach(() => {
        mockGet.mockReset();
        mockPost.mockReset();
    });

    it("posts to /companies and invalidates the companies list", async () => {
        mockGet.mockResolvedValue(COMPANIES);
        mockPost.mockResolvedValue({ id: "3", name: "Новая", description: "", callLogCount: 0, practiceCallCount: 0, createdAt: "", updatedAt: "" });
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useCreateCompany(), { wrapper });

        result.current.mutate({ name: "Новая" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies", { name: "Новая" });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
    });
});

describe("useDeleteCompany", () => {
    beforeEach(() => {
        mockDelete.mockReset();
    });

    it("calls DELETE /companies/{id} and invalidates the list", async () => {
        mockDelete.mockResolvedValue(undefined);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useDeleteCompany(), { wrapper });
        result.current.mutate("1");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockDelete).toHaveBeenCalledWith("/companies/1");
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
    });
});
