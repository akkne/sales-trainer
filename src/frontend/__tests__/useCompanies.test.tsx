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
    useCompanies,
    useCreateCompany,
    useUpdateCompany,
    useUpdateCompanyStatus,
    useUpdateCompanyFollowUp,
    useDeleteCompany,
    type CompanySummary,
} from "@/features/companies/hooks/use-companies";
import type { CompanyStatus } from "@/features/companies/lib/company-status";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;
const mockPut = apiClient.put as ReturnType<typeof vi.fn>;
const mockDelete = apiClient.delete as ReturnType<typeof vi.fn>;

const COMPANIES: CompanySummary[] = [
    { id: "1", name: "Ромашка", descriptionExcerpt: "", status: "Lead", contactCount: 0, callLogCount: 0, practiceCallCount: 0, nextActionAt: null, createdAt: "", updatedAt: "2026-07-01T00:00:00Z" },
    { id: "2", name: "Вектор", descriptionExcerpt: "", status: "Lead", contactCount: 0, callLogCount: 0, practiceCallCount: 0, nextActionAt: null, createdAt: "", updatedAt: "2026-07-01T00:00:00Z" },
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

describe("useUpdateCompany", () => {
    beforeEach(() => {
        mockPut.mockReset();
        toastError.mockReset();
    });

    it("shows an error toast when the update fails", async () => {
        mockPut.mockRejectedValue(new Error("network down"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useUpdateCompany(), { wrapper });
        result.current.mutate({ id: "1", name: "Ромашка", description: "" });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("network down"));
    });
});

describe("useUpdateCompanyStatus", () => {
    beforeEach(() => {
        mockPut.mockReset();
        toastError.mockReset();
    });

    it("puts to /companies/{id}/status and invalidates list and detail caches", async () => {
        mockPut.mockResolvedValue({ id: "1", name: "Ромашка", description: "", status: "Contacted", contactCount: 0, callLogCount: 0, practiceCallCount: 0, createdAt: "", updatedAt: "" });
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result.current.mutate({ id: "1", status: "Contacted" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPut).toHaveBeenCalledWith("/companies/1/status", { status: "Contacted" });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "1"] });
    });

    it("shows an error toast when the status update fails", async () => {
        mockPut.mockRejectedValue(new Error("network down"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result.current.mutate({ id: "1", status: "DealLost" });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("network down"));
    });

    it("optimistically flips the status in the cached list before the request resolves", async () => {
        mockGet.mockResolvedValue(COMPANIES);
        const { queryClient, wrapper } = createWrapper();

        const { result: listResult } = renderHook(() => useCompanies(), { wrapper });
        await waitFor(() => expect(listResult.current.isSuccess).toBe(true));

        let resolvePut!: (value: unknown) => void;
        mockPut.mockReturnValue(new Promise((resolve) => { resolvePut = resolve; }));

        const { result } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result.current.mutate({ id: "1", status: "Contacted" });

        await waitFor(() =>
            expect(queryClient.getQueryData<CompanySummary[]>(["companies"])?.find((c) => c.id === "1")?.status).toBe(
                "Contacted"
            )
        );

        resolvePut({ id: "1", name: "Ромашка", description: "", status: "Contacted", contactCount: 0, callLogCount: 0, practiceCallCount: 0, createdAt: "", updatedAt: "" });
        await waitFor(() => expect(result.current.isSuccess).toBe(true));
    });

    it("rolls back the optimistic status change on error", async () => {
        mockGet.mockResolvedValue(COMPANIES);
        const { queryClient, wrapper } = createWrapper();

        const { result: listResult } = renderHook(() => useCompanies(), { wrapper });
        await waitFor(() => expect(listResult.current.isSuccess).toBe(true));

        mockPut.mockRejectedValue(new Error("network down"));

        const { result } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result.current.mutate({ id: "1", status: "Contacted" });

        await waitFor(() => expect(result.current.isError).toBe(true));

        expect(queryClient.getQueryData<CompanySummary[]>(["companies"])?.find((c) => c.id === "1")?.status).toBe(
            "Lead"
        );
    });

    it("optimistically flips the status in the cached company detail, and rolls back on error", async () => {
        const { queryClient, wrapper } = createWrapper();
        const detail = {
            id: "1", name: "Ромашка", description: "", status: "Lead" as CompanyStatus,
            contactCount: 0, callLogCount: 0, practiceCallCount: 0,
            nextActionAt: null, nextActionNote: null, followUpNotifiedAt: null,
            createdAt: "", updatedAt: "",
        };
        queryClient.setQueryData(["companies", "1"], detail);

        let resolvePut!: (value: unknown) => void;
        mockPut.mockReturnValue(new Promise((resolve) => { resolvePut = resolve; }));

        const { result } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result.current.mutate({ id: "1", status: "Contacted" });

        await waitFor(() =>
            expect(queryClient.getQueryData<typeof detail>(["companies", "1"])?.status).toBe("Contacted")
        );

        resolvePut({ ...detail, status: "Contacted" });
        await waitFor(() => expect(result.current.isSuccess).toBe(true));

        // Now exercise the rollback path on a fresh mutation.
        mockPut.mockRejectedValue(new Error("network down"));
        const { result: result2 } = renderHook(() => useUpdateCompanyStatus(), { wrapper });
        result2.current.mutate({ id: "1", status: "DealLost" });

        await waitFor(() => expect(result2.current.isError).toBe(true));
        expect(queryClient.getQueryData<typeof detail>(["companies", "1"])?.status).toBe("Contacted");
    });
});

describe("useUpdateCompanyFollowUp", () => {
    beforeEach(() => {
        mockPut.mockReset();
        toastError.mockReset();
    });

    it("puts to /companies/{id}/follow-up and invalidates list and detail caches", async () => {
        mockPut.mockResolvedValue({
            id: "1", name: "Ромашка", description: "", status: "Lead",
            contactCount: 0, callLogCount: 0, practiceCallCount: 0,
            nextActionAt: "2026-08-01T00:00:00Z", nextActionNote: "Позвонить", followUpNotifiedAt: null,
            createdAt: "", updatedAt: "",
        });
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useUpdateCompanyFollowUp(), { wrapper });
        result.current.mutate({ id: "1", nextActionAt: "2026-08-01T00:00:00Z", nextActionNote: "Позвонить" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPut).toHaveBeenCalledWith("/companies/1/follow-up", {
            nextActionAt: "2026-08-01T00:00:00Z",
            nextActionNote: "Позвонить",
        });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "1"] });
    });

    it("supports clearing the follow-up by sending null fields", async () => {
        mockPut.mockResolvedValue({
            id: "1", name: "Ромашка", description: "", status: "Lead",
            contactCount: 0, callLogCount: 0, practiceCallCount: 0,
            nextActionAt: null, nextActionNote: null, followUpNotifiedAt: null,
            createdAt: "", updatedAt: "",
        });
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useUpdateCompanyFollowUp(), { wrapper });
        result.current.mutate({ id: "1", nextActionAt: null, nextActionNote: null });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPut).toHaveBeenCalledWith("/companies/1/follow-up", { nextActionAt: null, nextActionNote: null });
    });

    it("shows an error toast when the follow-up update fails", async () => {
        mockPut.mockRejectedValue(new Error("network down"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useUpdateCompanyFollowUp(), { wrapper });
        result.current.mutate({ id: "1", nextActionAt: "2026-08-01T00:00:00Z", nextActionNote: null });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("network down"));
    });
});

describe("useDeleteCompany", () => {
    beforeEach(() => {
        mockDelete.mockReset();
        toastError.mockReset();
    });

    it("calls DELETE /companies/{id}, invalidates the list, and drops the detail cache entry", async () => {
        mockDelete.mockResolvedValue(undefined);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
        const removeSpy = vi.spyOn(queryClient, "removeQueries");

        const { result } = renderHook(() => useDeleteCompany(), { wrapper });
        result.current.mutate("1");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockDelete).toHaveBeenCalledWith("/companies/1");
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
        expect(removeSpy).toHaveBeenCalledWith({ queryKey: ["companies", "1"] });
    });

    it("shows an error toast when the delete fails", async () => {
        mockDelete.mockRejectedValue(new Error("boom"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useDeleteCompany(), { wrapper });
        result.current.mutate("1");

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("boom"));
    });
});
