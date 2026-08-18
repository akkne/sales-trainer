"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

const SKILL_GAPS_STALE_TIME_MILLISECONDS = 60_000;

export interface TeamSkillGapSkill {
    skillId: string;
    title: string;
    attemptCount: number;
    accuracyPercent: number;
}

export interface TeamSkillGap {
    stageKey: string;
    stageLabel: string;
    /// `skill-gap:<stage>@<date>`, assembled by the code that measured the gap. The panel carries
    /// it, never builds it.
    sourceRef: string;
    attemptCount: number;
    accuracyPercent: number;
    strugglingManagerCount: number;
    measuredManagerCount: number;
    weakestSkills: TeamSkillGapSkill[];
    proposedTitle: string;
    proposedGoal: string;
}

export interface SuppressedTeamSkillGap {
    stageKey: string;
    stageLabel: string;
    attemptCount: number;
    accuracyPercent: number;
    /// One of `TeamSkillGapSuppressionReasons`.
    reason: string;
    suppressedUntil: string | null;
    contentGenerationJobId: string | null;
}

export interface TeamSkillGaps {
    windowStart: string;
    minimumAttemptsForGap: number;
    maximumAccuracyPercentForGap: number;
    minimumStrugglingManagers: number;
    gaps: TeamSkillGap[];
    /// Stages that do qualify as a failure and are deliberately not being offered. Rendered even
    /// when `gaps` is empty: a panel that shows nothing is indistinguishable from a broken one.
    suppressed: SuppressedTeamSkillGap[];
    rosterKnown: boolean;
}

export interface ContentGenerationJob {
    id: string;
    title: string;
    status: string;
}

const buildSkillGapsQueryKey = (windowDays: number) =>
    ["org", "team", "skill-gaps", windowDays] as const;

/// What the dashboard proposes doing next, over the same window the heat map was drawn over.
export function useTeamSkillGaps(windowDays: number) {
    return useQuery<TeamSkillGaps>({
        queryKey: buildSkillGapsQueryKey(windowDays),
        queryFn: () => apiClient.get<TeamSkillGaps>(`/admin/team/skill-gaps?days=${windowDays}`),
        staleTime: SKILL_GAPS_STALE_TIME_MILLISECONDS,
    });
}

/// The button. Answers with a run in `structuring` or `insufficient`, never with a finished lesson —
/// the caller is expected to leave for the checkpoint screen rather than to wait here.
///
/// Asking twice for a stage that already has a live run returns that same run, so a double click
/// costs nothing and lands in the same place.
export function useStartGapContentGeneration() {
    return useMutation<ContentGenerationJob, Error, string>({
        mutationFn: (stageKey: string) =>
            apiClient.post<ContentGenerationJob>(
                `/admin/team/skill-gaps/${encodeURIComponent(stageKey)}/content`,
                {}
            ),
    });
}

interface DismissTeamSkillGapVariables {
    stageKey: string;
    note?: string;
}

/// «Не сейчас». The recomputed panel comes back in the response, so the offer disappears without a
/// second read.
export function useDismissTeamSkillGap(windowDays: number) {
    const queryClient = useQueryClient();

    return useMutation<TeamSkillGaps, Error, DismissTeamSkillGapVariables>({
        mutationFn: ({ stageKey, note }) =>
            apiClient.post<TeamSkillGaps>(
                `/admin/team/skill-gaps/${encodeURIComponent(stageKey)}/dismiss`,
                { note: note ?? null }
            ),
        onSuccess: (recomputedGaps) => {
            queryClient.setQueryData(buildSkillGapsQueryKey(windowDays), recomputedGaps);
        },
    });
}

/// Takes a refusal back before it expires. Answers 204, so the panel has to re-read.
export function useRestoreTeamSkillGap(windowDays: number) {
    const queryClient = useQueryClient();

    return useMutation<void, Error, string>({
        mutationFn: (stageKey: string) =>
            apiClient.delete<void>(
                `/admin/team/skill-gaps/${encodeURIComponent(stageKey)}/dismiss`
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: buildSkillGapsQueryKey(windowDays) });
        },
    });
}
