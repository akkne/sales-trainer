"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { organizationPeopleService } from "@/features/org-people/services/organization-people-service";
import type {
    CreateInvitesRequest,
    CreateInvitesResponse,
    InviteStatusFilter,
    MembershipStatusFilter,
    OrganizationInvite,
    OrganizationMember,
} from "@/features/org-people/types/organization-people";

const ORGANIZATION_PEOPLE_QUERY_KEY = ["org", "people"];

const membershipsQueryKey = (status: MembershipStatusFilter) => [
    ...ORGANIZATION_PEOPLE_QUERY_KEY,
    "memberships",
    status,
];

const invitesQueryKey = (status: InviteStatusFilter) => [
    ...ORGANIZATION_PEOPLE_QUERY_KEY,
    "invites",
    status,
];

const FORBIDDEN_MESSAGE =
    "Приглашать и отключать людей может только суперадминистратор организации.";
const GENERIC_FAILURE_MESSAGE = "Не удалось выполнить действие. Попробуйте ещё раз.";

/// A failed write, in a sentence the РОП can act on. A 403 here is not a bug to retry: both reads
/// on this screen are open to a `TenancyAdmin` and none of the writes are, so somebody who can see
/// the screen can legitimately be unable to change it.
export function describePeopleWriteFailure(error: unknown): string {
    if (error instanceof ApiError) {
        if (error.status === 403) return FORBIDDEN_MESSAGE;
        if (error.status === 404) return "Запись уже изменилась — обновите страницу.";
        if (typeof error.payload.message === "string" && error.payload.message.length > 0) {
            return error.payload.message;
        }
    }
    return GENERIC_FAILURE_MESSAGE;
}

/// Who works here. Defaults to the people who still do; `all` adds the ones who were offboarded,
/// whose rows are kept forever and therefore have to be asked for.
export function useOrganizationMembers(status: MembershipStatusFilter) {
    return useQuery<OrganizationMember[]>({
        queryKey: membershipsQueryKey(status),
        queryFn: () => organizationPeopleService.listMemberships(status),
    });
}

/// Who has been invited and has not arrived yet. `all` adds accepted, revoked and expired invites,
/// which is the only place the four-state vocabulary is visible at once.
export function useOrganizationInvites(status: InviteStatusFilter) {
    return useQuery<OrganizationInvite[]>({
        queryKey: invitesQueryKey(status),
        queryFn: () => organizationPeopleService.listInvites(status),
    });
}

/// Bulk invite. The invite list is refetched afterwards, but the response itself is what the screen
/// renders: it is the only place that says which of the pasted addresses were refused and why, and
/// a refetch of `GET /invites` cannot reconstruct that — a refused address left no row.
export function useCreateInvites() {
    const queryClient = useQueryClient();

    return useMutation<CreateInvitesResponse, unknown, CreateInvitesRequest>({
        mutationFn: (request) => organizationPeopleService.createInvites(request),
        onSuccess: (response) => {
            clientLogger.info("Organization invites created", {
                createdCount: response.created.length,
                rejectedCount: response.rejected.length,
            });
            queryClient.invalidateQueries({ queryKey: ORGANIZATION_PEOPLE_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to create organization invites", {
                error: (error as Error).message,
            });
        },
    });
}

export function useRevokeInvite() {
    const queryClient = useQueryClient();

    return useMutation<void, unknown, string>({
        mutationFn: (inviteId) => organizationPeopleService.revokeInvite(inviteId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ORGANIZATION_PEOPLE_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to revoke organization invite", {
                error: (error as Error).message,
            });
        },
    });
}

/// Deactivation, never deletion. Both lists are invalidated: the person leaves the active roster
/// and appears under «все», and nothing else on the panel changes.
export function useDeactivateMembership() {
    const queryClient = useQueryClient();

    return useMutation<void, unknown, string>({
        mutationFn: (userId) => organizationPeopleService.deactivateMembership(userId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ORGANIZATION_PEOPLE_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to deactivate organization membership", {
                error: (error as Error).message,
            });
        },
    });
}
