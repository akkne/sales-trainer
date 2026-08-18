"use client";

import { useQueries } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

const BADGE_STALE_TIME_MILLISECONDS = 60_000;

export interface OrganizationNavigationBadgeCounts {
    /** Assignments currently issued to the team. `0` renders no badge. */
    activeAssignmentCount: number;
    /** Score disputes nobody has answered yet. `0` renders no badge. */
    openScoreDisputeCount: number;
    /** Any override left behind by a newer platform version — a dot, never a number. */
    hasStaleContent: boolean;
}

interface IdentifiedRecord {
    id: string;
}

/**
 * The three counters of the organization sidebar, and the only counters in the whole panel
 * (docs/TENANCY/ADMIN_UI_DESIGN.md §1.6). Each answers one question — «есть ли там для меня
 * работа» — which is why the content one is a dot and not a number: knowing that something went
 * stale is actionable, knowing that eleven things did is not.
 *
 * Three requests per visit to the panel, because the aggregating endpoint that would replace them
 * does not exist. `refetchOnWindowFocus` is what makes the counters catch up after the РОП closes
 * an assignment in another tab.
 *
 * A failing request contributes nothing rather than a zero or a dot: ai-service being down must
 * not light «Контент», because a dot that means "we could not ask" sends somebody looking for
 * work that is not there.
 */
export function useOrganizationNavigationBadges(
    isEnabled: boolean
): OrganizationNavigationBadgeCounts {
    const results = useQueries({
        queries: [
            {
                queryKey: ["org", "nav-badges", "assignments"],
                queryFn: () =>
                    apiClient.get<IdentifiedRecord[]>("/admin/assignments?status=active"),
                staleTime: BADGE_STALE_TIME_MILLISECONDS,
                refetchOnWindowFocus: true,
                enabled: isEnabled,
                retry: false,
            },
            {
                queryKey: ["org", "nav-badges", "score-disputes"],
                queryFn: () =>
                    apiClient.get<IdentifiedRecord[]>(
                        "/admin/dialog-reviews?kind=score_dispute&status=open"
                    ),
                staleTime: BADGE_STALE_TIME_MILLISECONDS,
                refetchOnWindowFocus: true,
                enabled: isEnabled,
                retry: false,
            },
            {
                queryKey: ["org", "nav-badges", "stale-content-overrides"],
                queryFn: () =>
                    apiClient.get<IdentifiedRecord[]>("/admin/content/overrides?staleOnly=true"),
                staleTime: BADGE_STALE_TIME_MILLISECONDS,
                refetchOnWindowFocus: true,
                enabled: isEnabled,
                retry: false,
            },
            {
                queryKey: ["org", "nav-badges", "stale-mode-overrides"],
                queryFn: () =>
                    apiClient.get<IdentifiedRecord[]>(
                        "/admin/dialog/overrides/modes?staleOnly=true"
                    ),
                staleTime: BADGE_STALE_TIME_MILLISECONDS,
                refetchOnWindowFocus: true,
                enabled: isEnabled,
                retry: false,
            },
        ],
    });

    const [assignments, scoreDisputes, staleContentOverrides, staleModeOverrides] = results;

    return {
        activeAssignmentCount: assignments.data?.length ?? 0,
        openScoreDisputeCount: scoreDisputes.data?.length ?? 0,
        hasStaleContent:
            (staleContentOverrides.data?.length ?? 0) > 0 ||
            (staleModeOverrides.data?.length ?? 0) > 0,
    };
}
