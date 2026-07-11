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
    useCompanyContacts,
    useAddCompanyContact,
    useUpdateCompanyContact,
    useDeleteCompanyContact,
} from "@/features/companies/hooks/use-company-contacts";

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

const CONTACT = {
    id: "contact-1", companyId: "c1", name: "Иван Петров", position: "Руководитель закупок",
    notes: "Любит цифры", createdAt: "", updatedAt: "",
};

describe("useCompanyContacts", () => {
    beforeEach(() => mockGet.mockReset());

    it("fetches contacts from /companies/{id}/contacts", async () => {
        mockGet.mockResolvedValue([CONTACT]);
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useCompanyContacts("c1"), { wrapper });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/companies/c1/contacts");
        expect(result.current.data).toHaveLength(1);
    });
});

describe("useAddCompanyContact", () => {
    beforeEach(() => {
        mockPost.mockReset();
        toastError.mockReset();
    });

    it("posts the contact and invalidates contacts/company/companies queries", async () => {
        mockPost.mockResolvedValue(CONTACT);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useAddCompanyContact("c1"), { wrapper });
        result.current.mutate({ name: "Иван Петров", position: "Руководитель закупок", notes: "Любит цифры" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPost).toHaveBeenCalledWith("/companies/c1/contacts", expect.objectContaining({ name: "Иван Петров" }));
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "contacts"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1"] });
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies"] });
    });

    it("shows an error toast when the request fails", async () => {
        mockPost.mockRejectedValue(new Error("boom"));
        const { wrapper } = createWrapper();

        const { result } = renderHook(() => useAddCompanyContact("c1"), { wrapper });
        result.current.mutate({ name: "Иван", position: "", notes: "" });

        await waitFor(() => expect(result.current.isError).toBe(true));
        expect(toastError).toHaveBeenCalledWith(expect.stringContaining("boom"));
    });
});

describe("useUpdateCompanyContact", () => {
    beforeEach(() => mockPut.mockReset());

    it("puts to /companies/{id}/contacts/{contactId} and invalidates the contacts list", async () => {
        mockPut.mockResolvedValue(CONTACT);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useUpdateCompanyContact("c1"), { wrapper });
        result.current.mutate({ contactId: "contact-1", name: "Иван Петров", position: "Директор", notes: "" });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockPut).toHaveBeenCalledWith("/companies/c1/contacts/contact-1", expect.objectContaining({ name: "Иван Петров" }));
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "contacts"] });
    });
});

describe("useDeleteCompanyContact", () => {
    beforeEach(() => mockDelete.mockReset());

    it("deletes the contact and invalidates contacts/company/companies queries", async () => {
        mockDelete.mockResolvedValue(undefined);
        const { queryClient, wrapper } = createWrapper();
        const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

        const { result } = renderHook(() => useDeleteCompanyContact("c1"), { wrapper });
        result.current.mutate("contact-1");

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockDelete).toHaveBeenCalledWith("/companies/c1/contacts/contact-1");
        expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["companies", "c1", "contacts"] });
    });
});
