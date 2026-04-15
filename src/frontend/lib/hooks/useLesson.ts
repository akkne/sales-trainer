import { useMutation, useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";
import type { ExerciseType } from "@/lib/exerciseTypes";

export interface LessonSummary {
    lessonId: string;
    title: string;
    orderInTopic: number;
    status: "locked" | "available" | "in_progress" | "completed";
    bestScore: number;
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
