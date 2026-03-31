import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface SkillTreeNode {
    skillId: string;
    slug: string;
    title: string;
    iconName: string;
    sortOrder: number;
    status: "locked" | "available" | "in_progress" | "completed";
    completedLessonCount: number;
    totalLessonCount: number;
    isLocked: boolean;
}

export interface SkillTreeData {
    skillNodes: SkillTreeNode[];
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
}

export function useSkillTree() {
    return useQuery({
        queryKey: ["skill-tree"],
        queryFn: () => apiClient.get<SkillTreeData>("/skill-tree"),
    });
}
