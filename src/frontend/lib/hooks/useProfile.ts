import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface UserProfileStats {
    displayName: string;
    email: string;
    currentStreakDayCount: number;
    longestStreakDayCount: number;
    totalXpAmount: number;
    completedSkillCount: number;
    totalSkillCount: number;
    averageExerciseScore: number;
}

export function useProfile() {
    return useQuery({
        queryKey: ["profile"],
        queryFn: () => apiClient.get<UserProfileStats>("/profile"),
    });
}
