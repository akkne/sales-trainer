"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";

/**
 * Phase 40.9 — the platform superadmin screen.
 *
 * Two backends sit behind it and that split is deliberate: organization-service owns the tenant
 * registry (`/organizations`), identity-service owns anything that needs identity-db — minting a
 * token and creating an invite (`/admin/platform/*`). Both are gated by `RequireSuperAdmin` — inviting an admin adds a user, and impersonation is superadmin-exclusive (docs/DECISIONS.md, 2026-08-16).
 *
 * `useBootstrapOrganizationAdmin` is not once-per-organization: an organization may hold any number
 * of admins and superadmins (docs/DECISIONS.md, 2026-08-27), so this mutation is callable as often
 * as the customer needs staffing. A duplicate address is a `400` from the ordinary invite rules.
 */

export type OrganizationStatus = "Active" | "Suspended";

export interface PlatformOrganization {
    id: string;
    name: string;
    slug: string;
    status: OrganizationStatus;
    createdAt: string;
}

export interface OrganizationReference {
    id: string;
    name: string;
}

export type OrganizationAdminRole = "TenancyAdmin" | "TenancySuperAdmin";

export interface BootstrapOrganizationAdminResult {
    inviteId: string;
    organization: OrganizationReference;
    email: string;
    expiresAt: string;
    token: string;
}

export interface ImpersonationToken {
    accessToken: string;
    expiresAt: string;
    impersonationId: string;
    organization: OrganizationReference;
}

export interface ImpersonationAuditEntry {
    id: string;
    actorUserId: string;
    actorEmail: string;
    organization: OrganizationReference;
    reason: string;
    issuedAt: string;
    expiresAt: string;
}

const organizationsQueryKey = ["admin", "organizations"];
const impersonationsQueryKey = ["admin", "impersonations"];

export function usePlatformOrganizations() {
    return useQuery({
        queryKey: organizationsQueryKey,
        queryFn: () => apiClient.get<PlatformOrganization[]>("/organizations"),
    });
}

export function useCreateOrganization() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ name, slug }: { name: string; slug?: string }) =>
            apiClient.post<PlatformOrganization>("/organizations", { name, slug: slug || null }),
        onSuccess: (organization) => {
            clientLogger.info("Organization created", {
                organizationId: organization.id,
                slug: organization.slug,
            });
            queryClient.invalidateQueries({ queryKey: organizationsQueryKey });
        },
        onError: (error) => {
            clientLogger.error("Failed to create organization", { error: (error as Error).message });
        },
    });
}

export function useSetOrganizationStatus() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, status }: { id: string; status: OrganizationStatus }) =>
            apiClient.post<PlatformOrganization>(
                `/organizations/${id}/${status === "Suspended" ? "suspend" : "reactivate"}`,
                {}
            ),
        onSuccess: (_, variables) => {
            clientLogger.warn("Organization status changed", {
                organizationId: variables.id,
                status: variables.status,
            });
            queryClient.invalidateQueries({ queryKey: organizationsQueryKey });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to change organization status", {
                organizationId: variables.id,
                error: (error as Error).message,
            });
        },
    });
}

export function useBootstrapOrganizationAdmin() {
    return useMutation({
        mutationFn: ({
            organizationId,
            email,
            role = "TenancySuperAdmin",
        }: {
            organizationId: string;
            email: string;
            role?: OrganizationAdminRole;
        }) =>
            apiClient.post<BootstrapOrganizationAdminResult>(
                "/admin/platform/organizations/bootstrap-admin",
                { organizationId, email, role }
            ),
        onSuccess: (result) => {
            clientLogger.info("Organization administrator invited", {
                organizationId: result.organization.id,
                inviteId: result.inviteId,
            });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to invite an organization administrator", {
                organizationId: variables.organizationId,
                error: (error as Error).message,
            });
        },
    });
}

export function useImpersonationAudit() {
    return useQuery({
        queryKey: impersonationsQueryKey,
        queryFn: () => apiClient.get<ImpersonationAuditEntry[]>("/admin/platform/impersonation"),
    });
}

export function useStartImpersonation() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ organizationId, reason }: { organizationId: string; reason: string }) =>
            apiClient.post<ImpersonationToken>("/admin/platform/impersonation", {
                organizationId,
                reason,
            }),
        onSuccess: (issuedToken) => {
            clientLogger.warn("Impersonation started", {
                organizationId: issuedToken.organization.id,
                impersonationId: issuedToken.impersonationId,
                expiresAt: issuedToken.expiresAt,
            });
            queryClient.invalidateQueries({ queryKey: impersonationsQueryKey });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to start impersonation", {
                organizationId: variables.organizationId,
                error: (error as Error).message,
            });
        },
    });
}
