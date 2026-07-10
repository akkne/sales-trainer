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
    useCompanyPersonas,
    useAddCompanyPersona,
    useDeleteCompanyPersona,
    useGenerateCompanyPersona,
} from "@/features/companies/hooks/use-company-personas";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;
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

const PERSONA = {
    id: "persona-1", companyId: "c1", name: "Мария Соколова", position: "Руководитель закупок",
    personality: "Прагматична.", difficulty: "Hard" as const, createdAt: "",
};

describe("useCompanyPersonas", () => {
    beforeEach(() => mockGet.mockReset());

    it("fetches personas from /companies/{id}/personas", async () => {
        mockGet.mockResolvedValue([PERSONA]);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyPersonas("c1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/c1/personas");
        expect(result.current.data).toHaveLength(1);
    });
});

describe("useAddCompanyPersona", () => {
    beforeEach(() => {
        mockPost.mockReset();
        toastError.mockReset();
    });

    it("posts the persona and invalidates the personas query", async () => {
        mockPost.mockResolvedValue(PERSONA);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useAddCompanyPersona("c1"), { wrapper });
        result.current.mutate({ name: "Мария Соколова", position: "Руководитель закупок", personality: "Прагматична.", difficulty: "Hard" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/c1/personas", expect.objectContaining({ name: "Мария Соколова" }));
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "personas"] });
    });

    it("shows an error toast when the request fails", async () => {
        mockPost.mockRejectedValue(new Error("boom"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useAddCompanyPersona("c1"), { wrapper });
        result.current.mutate({ name: "Мария", position: "", personality: "", difficulty: "Medium" });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("boom"));
    });
});

describe("useDeleteCompanyPersona", () => {
    beforeEach(() => mockDelete.mockReset());

    it("deletes the persona and invalidates the personas query", async () => {
        mockDelete.mockResolvedValue(undefined);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useDeleteCompanyPersona("c1"), { wrapper });
        result.current.mutate("persona-1");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockDelete).toHaveBeenCalledWith("/companies/c1/personas/persona-1");
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "personas"] });
    });
});

describe("useGenerateCompanyPersona", () => {
    beforeEach(() => {
        mockPost.mockReset();
        toastError.mockReset();
    });

    it("posts to /companies/{id}/personas/generate and returns the draft", async () => {
        mockPost.mockResolvedValue({ name: "Мария", position: "Закупщик", personality: "Скептична." });
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useGenerateCompanyPersona("c1"), { wrapper });
        result.current.mutate({ difficulty: "Hard" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/c1/personas/generate", { difficulty: "Hard" });
        expect(result.current.data).toEqual({ name: "Мария", position: "Закупщик", personality: "Скептична." });
    });

    it("shows an error toast when generation fails", async () => {
        mockPost.mockRejectedValue(new Error("AI unavailable"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useGenerateCompanyPersona("c1"), { wrapper });
        result.current.mutate({ difficulty: "Medium" });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("AI unavailable"));
    });
});
