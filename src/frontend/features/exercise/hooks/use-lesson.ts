import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type { ExerciseType } from "@/features/exercise/types/exercise-types";

export interface LessonSummary {
    lessonId: string;
    title: string;
    orderInTopic: number;
    topicOrder: number;
    status: "locked" | "available" | "in_progress" | "completed";
    bestScore: number;
    // "theory" when every exercise in the lesson is a theory_card, otherwise "practice".
    kind: "theory" | "practice";
}

export interface ExerciseData {
    exerciseId: string;
    type: ExerciseType;
    sortOrder: number;
    content: unknown;
}

export interface ExerciseSubmissionResult {
    isCorrect: boolean;
    score: number;
    explanation: string | null;
    aiFeedback: string | null;
    xpEarned: number;
    newlyUnlockedAchievementKeys: string[];
}

export function useAllLessons() {
    return useQuery({
        queryKey: ["lessons", "all"],
        queryFn: () => apiClient.get<LessonSummary[]>(`/lessons`),
    });
}

export function useLessonsForSkill(skillSlug: string) {
    return useQuery({
        queryKey: ["lessons", skillSlug],
        queryFn: () => apiClient.get<LessonSummary[]>(`/skills/${skillSlug}/lessons`),
    });
}

export function useExercisesForLesson(lessonId: string) {
    return useQuery({
        queryKey: ["exercises", lessonId],
        queryFn: () => apiClient.get<ExerciseData[]>(`/lessons/${lessonId}/exercises`),
    });
}

export function useSubmitExercise() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({
            exerciseId,
            answer,
        }: {
            exerciseId: string;
            answer: unknown;
        }) =>
            apiClient.post<ExerciseSubmissionResult>(
                `/exercises/${exerciseId}/submit`,
                { answer }
            ),
        // Progress may have changed (lesson completed / next lesson unlocked), so drop
        // the cached lesson lists — the path/tree/skill views refetch fresh statuses.
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["lessons"] });
        },
    });
}

export function useTranscribeAudio() {
    return useMutation({
        mutationFn: (blob: Blob) => {
            const formData = new FormData();
            formData.append("file", blob, "recording.webm");
            return apiClient.postFile<{ text: string; language: string | null }>(
                "/transcription/transcribe",
                formData
            );
        },
    });
}
