import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface Achievement {
    achievementId: string;
    key: string;
    title: string;
    description: string;
    iconEmoji: string;
    isUnlocked: boolean;
    unlockedAt: string | null;
}

export function useAchievements() {
    return useQuery({
        queryKey: ["achievements"],
        queryFn: () => apiClient.get<Achievement[]>("/profile/achievements"),
    });
}
