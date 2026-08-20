import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

/**
 * The learner's headline progress numbers, from learning-service — the service that owns the lesson
 * and score rows they are computed from.
 *
 * Identity-service's `/profile` also carries `averageExerciseScore` / `completedSkillCount` fields,
 * but they are hard-coded zeros left over from the microservices split. Read progress from here, not
 * from there.
 *
 * `averageExerciseScore` is `null` when nothing has been completed yet, which the UI must render as
 * "—" rather than as 0%.
 */
export interface LearningProgressSummary {
    completedSkillCount: number;
    totalSkillCount: number;
    completedLessonCount: number;
    averageExerciseScore: number | null;
}

export function useProgressSummary() {
    return useQuery({
        queryKey: ["skills", "progress-summary"],
        queryFn: () => apiClient.get<LearningProgressSummary>("/skills/progress-summary"),
    });
}
