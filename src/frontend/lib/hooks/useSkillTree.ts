import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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

/** Returns ALL skills with current user's progress. Used for profile & tree. */
export function useSkills() {
    return useQuery({
        queryKey: ["skills"],
        queryFn: () => apiClient.get<SkillTreeNode[]>("/skills"),
    });
}

/**
 * Mutation: replace the user's enrolled skill set.
 * Pass the full list of slugs the user wants to keep enrolled.
 * sales-basics is always kept by the backend regardless.
 */
export function useUpdateEnrolledSkills() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (skillSlugs: string[]) =>
            apiClient.put<void>("/skills/enrolled", { skillSlugs }),
        onSuccess: () => {
            // Re-fetch skills so statuses update immediately
            queryClient.invalidateQueries({ queryKey: ["skills"] });
            queryClient.invalidateQueries({ queryKey: ["skill-tree"] });
        },
    });
}
