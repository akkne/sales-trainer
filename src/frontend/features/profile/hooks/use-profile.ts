import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface UserProfileStats {
    displayName: string;
    email: string;
    currentStreakDayCount: number;
    longestStreakDayCount: number;
    totalXpAmount: number;
    completedSkillCount: number;
    totalSkillCount: number;
    averageExerciseScore: number;
    persona: string | null;
    avatarUrl?: string | null;
}

export function useProfile() {
    return useQuery({
        queryKey: ["profile"],
        queryFn: () => apiClient.get<UserProfileStats>("/profile"),
    });
}
