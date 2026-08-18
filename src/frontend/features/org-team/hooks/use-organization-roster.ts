"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type { OrganizationMembership } from "@/features/org-team/types/organization-membership";

const ROSTER_STALE_TIME_MILLISECONDS = 300_000;

/// Who works here, asked of identity-service directly.
///
/// ADMIN_UI_DESIGN.md §6.1 was written before this route existed and describes a palliative built
/// out of «кто хоть что-то решал». `GET /memberships?status=all` replaces it: the heat map can now
/// show the person hired last week who has practised nothing, and can mark the person who left but
/// whose history the organization keeps.
///
/// `retry: false` and a soft failure are deliberate. The roster is an improvement on the screen,
/// not a precondition for it — if identity-service is unreachable the panel falls back to the
/// design's palliative rather than showing an error over a heat map that is perfectly true.
export function useOrganizationRoster() {
    return useQuery<OrganizationMembership[]>({
        queryKey: ["org", "team", "roster"],
        queryFn: () => apiClient.get<OrganizationMembership[]>("/memberships?status=all"),
        staleTime: ROSTER_STALE_TIME_MILLISECONDS,
        retry: false,
    });
}
