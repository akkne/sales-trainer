"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import type { SalesTeamSize } from "@/features/demo/types";
import type { DemoRequestStatus } from "@/features/admin/lib/demo-request-format";

/**
 * The platform-admin side of the demo-request pipeline (docs/DEMO_REQUEST.md, "Admin side").
 * `DemoRequest` is not tenant-scoped — a lead precedes any organization — so it is read and
 * worked entirely under `RequirePlatformAdmin`, never under an organization-scoped policy.
 */

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
