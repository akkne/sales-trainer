"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

const TEAM_SKILL_MAP_STALE_TIME_MILLISECONDS = 60_000;

/**
 * Same label other features fall back to for a person the directory has no name for
 * (features/org-people/utils/format-people.ts, features/org-team/utils/team-roster.ts,
 * features/org-dialogs/constants/dialog-review-dictionary.ts) — kept as its own local constant
 * here too, matching how each of those features owns its copy rather than sharing one.
 */
const UNNAMED_MEMBER_LABEL = "Без имени";

export interface TeamSkillMapCell {
    key: string;
    attemptCount: number;
    /** Null, never zero, below `minimumAttemptsForAccuracy` — two right answers out of two is not 100%. */
    accuracyPercent: number | null;
}

export interface TeamSkillMapStage {
    key: string;
    label: string;
    accent: string;
    order: number;
    attemptCount: number;
    accuracyPercent: number | null;
}

export interface TeamSkillMapSkill {
    skillId: string;
    title: string;
    stageKey: string;
    orderInTree: number;
    attemptCount: number;
    accuracyPercent: number | null;
}

export interface TeamSkillMapMember {
    userId: string;
    /**
     * Null for anybody without a row yet in identity-service's replicated `UserReplicas` table
     * (ordinary replication lag, not an error) — see
     * `TeamSkillMapMemberDto.DisplayName` on the backend. Never read this directly for display;
     * go through `useTeamMemberNames`, which resolves it to `UNNAMED_MEMBER_LABEL`.
     */
    displayName: string | null;
    isActiveMember: boolean;
    attemptCount: number;
    accuracyPercent: number | null;
    weakestStageKey: string | null;
    weakestSkillId: string | null;
    dialogCount: number;
    dialogAverageScore: number | null;
    stages: TeamSkillMapCell[];
    skills: TeamSkillMapCell[];
}

export interface TeamSkillMap {
    windowStart: string;
    stages: TeamSkillMapStage[];
    skills: TeamSkillMapSkill[];
    members: TeamSkillMapMember[];
    unattributedAttemptCount: number;
    minimumAttemptsForAccuracy: number;
    /** False while identity-service has no roster endpoint (ADMIN_UI_DESIGN.md §6.1). */
    rosterKnown: boolean;
}

export interface TeamMemberName {
    userId: string;
    /** Always a real string here — `useTeamMemberNames` has already resolved a missing name to `UNNAMED_MEMBER_LABEL`. */
    displayName: string;
    isActiveMember: boolean;
}

/**
 * The team matrix behind O1, read along whichever axis the caller needs.
 *
 * It lives in `org-shell` rather than in the team slice that draws it because three other slices
 * — conversations, people, and the assignment audience picker — need nothing from it but the
 * names, and making them wait for the heat map would be a three-way block over thirty lines.
 */
export function useTeamSkillMap(windowDays?: number) {
    const queryPath =
        windowDays === undefined
            ? "/admin/team/skill-map"
            : `/admin/team/skill-map?days=${windowDays}`;

    return useQuery<TeamSkillMap>({
        queryKey: ["org", "team", "skill-map", windowDays ?? null],
        queryFn: () => apiClient.get<TeamSkillMap>(queryPath),
        staleTime: TEAM_SKILL_MAP_STALE_TIME_MILLISECONDS,
    });
}

/**
 * The team's names, alphabetical, active members first.
 *
 * Until identity-service grows `GET /memberships` this is the only roster the panel has, and it
 * only knows the people who have attempted something — `rosterKnown` on the response says so, and
 * every screen that lists people has to repeat that caveat rather than present this as complete.
 */
export function useTeamMemberNames(windowDays?: number) {
    const skillMapQuery = useTeamSkillMap(windowDays);

    const memberNames = useMemo<TeamMemberName[]>(() => {
        const members = skillMapQuery.data?.members ?? [];
        return members
            .map((member) => ({
                userId: member.userId,
                displayName: member.displayName ?? UNNAMED_MEMBER_LABEL,
                isActiveMember: member.isActiveMember,
            }))
            .sort((left, right) => {
                if (left.isActiveMember !== right.isActiveMember) {
                    return left.isActiveMember ? -1 : 1;
                }
                return left.displayName.localeCompare(right.displayName, "ru");
            });
    }, [skillMapQuery.data]);

    return {
        memberNames,
        isRosterKnown: skillMapQuery.data?.rosterKnown ?? false,
        isLoading: skillMapQuery.isLoading,
        isError: skillMapQuery.isError,
    };
}
