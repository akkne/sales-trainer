import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface LeagueParticipant {
    userId: string;
    displayName: string;
    weeklyXpAmount: number;
    rank: number;
    isCurrentUser: boolean;
}

export interface CurrentLeagueData {
    leagueId: string;
    tier: string;
    weekStartDate: string;
    weekEndDate: string;
    participantsByRank: LeagueParticipant[];
    currentUserRank: number;
    previousWeekOutcome: "promoted" | "demoted" | null;
}

export function useCurrentLeague() {
    return useQuery({
        queryKey: ["league"],
        queryFn: () => apiClient.get<CurrentLeagueData>("/league"),
    });
}
