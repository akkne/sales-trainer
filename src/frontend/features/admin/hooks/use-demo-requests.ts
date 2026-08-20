"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient, ApiError } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import type { SalesTeamSize } from "@/features/demo/types";
import type { DemoRequestStatus } from "@/features/admin/lib/demo-request-format";
import type { OrganizationAdminRole } from "@/features/admin/hooks/use-organizations";

/**
 * The platform-admin side of the demo-request pipeline (docs/DEMO_REQUEST.md, "Admin side").
 * `DemoRequest` is not tenant-scoped — a lead precedes any organization — so it is read and
 * worked entirely under `RequirePlatformAdmin`, never under an organization-scoped policy.
 */

/// Where a lead sits on the way from "approved" to "a real tenant with somebody who can sign
/// in", per `POST /admin/demo-requests/{id}/provision` (docs/DEMO_REQUEST.md, "Provisioning").
/// `OrganizationCreated` is the deliberately-surfaced half-finished state: the tenant exists but
/// nobody was ever invited to it, because the invite send failed after the organization write
/// committed.
export type ProvisioningState = "NotProvisioned" | "OrganizationCreated" | "AdminInvited";

export interface DemoRequestDto {
    id: string;
    fullName: string;
    workEmail: string;
    phone: string;
    companyName: string;
    jobTitle: string | null;
    salesTeamSize: SalesTeamSize;
    comment: string | null;
    status: DemoRequestStatus;
    consentGivenAt: string;
    marketingConsentGivenAt: string | null;
    createdAt: string;
    updatedAt: string;
    organizationId: string | null;
    organizationName: string | null;
    organizationSlug: string | null;
    provisioningState: ProvisioningState;
    bootstrapInviteId: string | null;
    bootstrapAdminEmail: string | null;
    provisionedAt: string | null;
}

/// Distinct from `OrganizationReference` (used elsewhere in the admin panel) because that shape
/// carries no `slug` — the provisioning response is the one place a slug is handed back
/// straight from the write that minted it, which is also the only reliable source for it: the
/// tenant registry list omits it from nothing worse than not being asked for it here, but
/// `DemoRequestDto` never carries it at all (see the comment on `provisionedDetailsById` in the
/// page for why that matters).
export interface ProvisionedOrganizationReference {
    id: string;
    name: string;
    slug: string;
}

export interface ProvisionDemoRequestResult {
    demoRequestId: string;
    status: DemoRequestStatus;
    provisioningState: ProvisioningState;
    organization: ProvisionedOrganizationReference;
    inviteId: string;
    inviteEmail: string;
    inviteExpiresAt: string;
    alreadyProvisioned: boolean;
}

export interface ProvisionDemoRequestVariables {
    id: string;
    /// All four are optional overrides on top of the server's own defaults (lead's company name /
    /// a normalized slug / the lead's work email / `TenancySuperAdmin`) — see
    /// `confirmProvision` in the page for which of these the UI actually sends.
    organizationName?: string;
    slug?: string;
    adminEmail?: string;
    role?: OrganizationAdminRole;
}

const demoRequestsQueryKey = ["admin", "demo-requests"];

export function useAdminDemoRequests() {
    return useQuery({
        queryKey: demoRequestsQueryKey,
        queryFn: () => apiClient.get<DemoRequestDto[]>("/admin/demo-requests"),
    });
}

export function useUpdateDemoRequestStatus() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, status }: { id: string; status: DemoRequestStatus }) =>
            apiClient.patch<DemoRequestDto>(`/admin/demo-requests/${id}/status`, { status }),
        onSuccess: (updated) => {
            clientLogger.info("Demo request status changed", {
                demoRequestId: updated.id,
                status: updated.status,
            });
            queryClient.invalidateQueries({ queryKey: demoRequestsQueryKey });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to change demo request status", {
                demoRequestId: variables.id,
                status: variables.status,
                error: (error as Error).message,
            });
        },
    });
}

/**
 * Turns an approved lead into a real organization plus a bootstrap invite, in one call
 * (docs/DEMO_REQUEST.md, "Provisioning"). `SuperAdmin` only, mirroring `RequireSuperAdmin` on
 * the backend — the page gates the action the same way it gates first-admin bootstrap on the
 * organizations screen.
 *
 * A `503 invite-failed` still means the organization write committed — only the invite send
 * failed — so the list is refetched on that error too, not just on success, or the row would
 * keep showing `NotProvisioned` after the tenant already exists.
 */
export function useProvisionDemoRequest() {
    const queryClient = useQueryClient();
    return useMutation<ProvisionDemoRequestResult, ApiError, ProvisionDemoRequestVariables>({
        mutationFn: ({ id, ...overrides }) =>
            apiClient.post<ProvisionDemoRequestResult>(
                `/admin/demo-requests/${id}/provision`,
                overrides,
            ),
        onSuccess: (result) => {
            clientLogger.info("Demo request provisioned", {
                demoRequestId: result.demoRequestId,
                provisioningState: result.provisioningState,
                organizationId: result.organization.id,
                alreadyProvisioned: result.alreadyProvisioned,
            });
            queryClient.invalidateQueries({ queryKey: demoRequestsQueryKey });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to provision demo request", {
                demoRequestId: variables.id,
                status: error.status,
                error: error.message,
            });
            if (error.status === 503) {
                queryClient.invalidateQueries({ queryKey: demoRequestsQueryKey });
            }
        },
    });
}
