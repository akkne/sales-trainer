import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { SKILL_STAGES, type SkillStageMeta } from "@/features/skills/constants/skill-stages";

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
    stage: string;
    /** ISO-8601 UTC timestamp of last practice activity. null (or absent) means never practiced. */
    lastActivityAt: string | null;
}

export interface SkillTreeData {
    skillNodes: SkillTreeNode[];
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
    dailyXpAmount: number;
    dailyXpGoal: number;
}

export function useSkillTree() {
    return useQuery({
        queryKey: ["skill-tree"],
        queryFn: () => apiClient.get<SkillTreeData>("/skill-tree"),
    });
}

/**
 * Returns the admin-configured funnel stages (label/accent/order) for grouping
 * skills on the tree. Falls back to the built-in {@link SKILL_STAGES} defaults
 * while loading, on error, or when none are configured.
 */
export function useSkillStages(): { stages: readonly SkillStageMeta[]; isLoading: boolean } {
    const query = useQuery({
        queryKey: ["skill-stages"],
        queryFn: () => apiClient.get<SkillStageMeta[]>("/skills/stages"),
    });
    const stages = query.data && query.data.length > 0 ? query.data : SKILL_STAGES;
    return { stages, isLoading: query.isLoading };
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
const ALWAYS_ENROLLED_SLUG = "sales-basics";

export function useUpdateEnrolledSkills() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (skillSlugs: string[]) =>
            apiClient.put<void>("/skills/enrolled", { skillSlugs }),
        // Optimistically flip statuses so the toggle responds instantly instead of
        // waiting for the round-trip. Rolled back in onError.
        onMutate: async (skillSlugs: string[]) => {
            await queryClient.cancelQueries({ queryKey: ["skills"] });
            const previous = queryClient.getQueryData<SkillTreeNode[]>(["skills"]);
            const enrolled = new Set([...skillSlugs, ALWAYS_ENROLLED_SLUG]);
            queryClient.setQueryData<SkillTreeNode[]>(["skills"], (current) =>
                current?.map((skill) => {
                    const wantEnrolled = enrolled.has(skill.slug);
                    if (wantEnrolled && skill.status === "locked") {
                        return { ...skill, status: "available", isLocked: false };
                    }
                    if (!wantEnrolled && skill.status !== "locked") {
                        return { ...skill, status: "locked", isLocked: true };
                    }
                    return skill;
                })
            );
            return { previous };
        },
        onError: (_error, _vars, context) => {
            if (context?.previous) {
                queryClient.setQueryData(["skills"], context.previous);
            }
        },
        onSettled: () => {
            // Re-fetch so server-truth statuses replace the optimistic guess.
            queryClient.invalidateQueries({ queryKey: ["skills"] });
            queryClient.invalidateQueries({ queryKey: ["skill-tree"] });
        },
    });
}
