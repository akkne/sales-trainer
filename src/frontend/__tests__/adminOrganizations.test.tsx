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
    useBootstrapOrganizationAdmin,
    useCreateOrganization,
    usePlatformOrganizations,
    useSetOrganizationStatus,
    useStartImpersonation,
    type PlatformOrganization,
} from "@/features/admin/hooks/use-organizations";
import {
    beginImpersonationSession,
    clearImpersonationSession,
    isImpersonationExpired,
    readImpersonationSession,
} from "@/features/admin/lib/impersonation-session";

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPost = apiClient.post as ReturnType<typeof vi.fn>;

const ORGANIZATION_ID = "8b1b6f3a-0000-4000-8000-000000000001";

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const TestQueryWrapper = ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    TestQueryWrapper.displayName = "TestQueryWrapper";
    return TestQueryWrapper;
}

describe("platform organization hooks", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it("reads the tenant registry from organization-service", async () => {
        const organizations: PlatformOrganization[] = [
            {
                id: ORGANIZATION_ID,
                name: "Acme Sales",
                slug: "acme-sales",
                status: "Active",
                createdAt: "2026-08-15T00:00:00Z",
            },
        ];
        mockGet.mockResolvedValueOnce(organizations);

        const { result } = renderHook(() => usePlatformOrganizations(), { wrapper: createWrapper() });

        await waitFor(() => expect(result.current.isSuccess).toBe(true));
        expect(mockGet).toHaveBeenCalledWith("/organizations");
        expect(result.current.data).toEqual(organizations);
    });

    it("creates an organization without ever sending an organization id", async () => {
        mockPost.mockResolvedValueOnce({
            id: ORGANIZATION_ID,
            name: "Acme Sales",
            slug: "acme-sales",
            status: "Active",
            createdAt: "2026-08-15T00:00:00Z",
        });

        const { result } = renderHook(() => useCreateOrganization(), { wrapper: createWrapper() });
        await result.current.mutateAsync({ name: "Acme Sales" });

        expect(mockPost).toHaveBeenCalledWith("/organizations", { name: "Acme Sales", slug: null });
    });

    it("suspends and resumes through the registry's own routes", async () => {
        mockPost.mockResolvedValue({});

        const { result } = renderHook(() => useSetOrganizationStatus(), { wrapper: createWrapper() });

        await result.current.mutateAsync({ id: ORGANIZATION_ID, status: "Suspended" });
        expect(mockPost).toHaveBeenLastCalledWith(`/organizations/${ORGANIZATION_ID}/suspend`, {});

        await result.current.mutateAsync({ id: ORGANIZATION_ID, status: "Active" });
        expect(mockPost).toHaveBeenLastCalledWith(`/organizations/${ORGANIZATION_ID}/reactivate`, {});
    });

    it("invites the first tenancy superadmin through the platform endpoint, never through /invites", async () => {
        mockPost.mockResolvedValueOnce({
            inviteId: "invite-1",
            organization: { id: ORGANIZATION_ID, name: "Acme Sales" },
            email: "admin@acme.com",
            expiresAt: "2026-08-22T00:00:00Z",
            token: "raw-token",
        });

        const { result } = renderHook(() => useBootstrapOrganizationAdmin(), { wrapper: createWrapper() });
        await result.current.mutateAsync({ organizationId: ORGANIZATION_ID, email: "admin@acme.com" });

        expect(mockPost).toHaveBeenCalledWith("/admin/platform/organizations/bootstrap-admin", {
            organizationId: ORGANIZATION_ID,
            email: "admin@acme.com",
        });
    });

    it("starts impersonation through the dedicated endpoint and always sends a reason", async () => {
        mockPost.mockResolvedValueOnce({
            accessToken: "impersonation-token",
            expiresAt: "2026-08-15T00:15:00Z",
            impersonationId: "audit-1",
            organization: { id: ORGANIZATION_ID, name: "Acme Sales" },
        });

        const { result } = renderHook(() => useStartImpersonation(), { wrapper: createWrapper() });
        const issuedToken = await result.current.mutateAsync({
            organizationId: ORGANIZATION_ID,
            reason: "support ticket 42",
        });

        expect(mockPost).toHaveBeenCalledWith("/admin/platform/impersonation", {
            organizationId: ORGANIZATION_ID,
            reason: "support ticket 42",
        });
        expect(issuedToken.accessToken).toBe("impersonation-token");
    });
});

describe("impersonation session bookkeeping", () => {
    beforeEach(() => {
        sessionStorage.clear();
    });

    it("parks the platform token so impersonation is not a one-way door", () => {
        beginImpersonationSession({
            platformAccessToken: "platform-token",
            organizationName: "Acme Sales",
            expiresAt: "2026-08-15T00:15:00Z",
        });

        expect(readImpersonationSession()).toEqual({
            platformAccessToken: "platform-token",
            organizationName: "Acme Sales",
            expiresAt: "2026-08-15T00:15:00Z",
        });

        clearImpersonationSession();
        expect(readImpersonationSession()).toBeNull();
    });

    it("reports an elapsed impersonation as expired", () => {
        const session = {
            platformAccessToken: "platform-token",
            organizationName: "Acme Sales",
            expiresAt: "2026-08-15T00:15:00Z",
        };

        expect(isImpersonationExpired(session, new Date("2026-08-15T00:14:00Z"))).toBe(false);
        expect(isImpersonationExpired(session, new Date("2026-08-15T00:16:00Z"))).toBe(true);
    });

    it("treats an unreadable expiry as expired rather than as an endless session", () => {
        expect(
            isImpersonationExpired({
                platformAccessToken: "platform-token",
                organizationName: "Acme Sales",
                expiresAt: "not-a-date",
            })
        ).toBe(true);
    });
});
