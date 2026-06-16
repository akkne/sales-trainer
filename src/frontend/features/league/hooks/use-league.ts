import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface LeagueParticipant {
    userId: string;
    displayName: string;
    weeklyXpAmount: number;
    rank: number;
    isCurrentUser: boolean;
    avatarUrl?: string | null;
}

export interface CurrentLeagueData {
    leagueId: string;
    tier: string;
    tierName: string;
    tierColor: string;
    weekStartDate: string;
    weekEndDate: string;
    periodEndsAt: string;
    participantsByRank: LeagueParticipant[];
    currentUserRank: number;
    previousWeekOutcome: "promoted" | "demoted" | null;
    promotionZoneSize: number;
    demotionZoneSize: number;
    maximumLeagueParticipantCount: number;
}

export function useCurrentLeague() {
    return useQuery({
        queryKey: ["league"],
        queryFn: () => apiClient.get<CurrentLeagueData>("/league"),
    });
}
