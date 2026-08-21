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

// The correct answer, revealed only after the learner has submitted one — never present on the
// pre-submission exercise content, which strips every answer-key field (docs/AUDIT_PROD.md
// X-3/X-6/X-8, docs/API_CONTRACTS.md). Each exercise type sets only the field it needs.
export interface ExerciseCorrectAnswer {
    correctOptionIndex: number | null;
    order: number[] | null;
    correctLineIndex: number | null;
}

export interface ExerciseSubmissionResult {
    isCorrect: boolean;
    score: number;
    explanation: string | null;
    aiFeedback: string | null;
    xpEarned: number;
    newlyUnlockedAchievementKeys: string[];
    correctAnswer: ExerciseCorrectAnswer | null;
}

export function useLessonsForSkill(skillSlug: string | undefined) {
    return useQuery({
        queryKey: ["lessons", skillSlug],
        queryFn: () => apiClient.get<LessonSummary[]>(`/skills/${skillSlug}/lessons`),
        enabled: !!skillSlug,
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
            skipped,
        }: {
            exerciseId: string;
            answer: unknown;
            // X-4: records a real, ungraded attempt on the server instead of only advancing
            // the client's own queue, so a lesson finished by skipping every remaining
            // exercise actually satisfies the backend's every-exercise-attempted completion gate.
            skipped?: boolean;
        }) =>
            apiClient.post<ExerciseSubmissionResult>(
                `/exercises/${exerciseId}/submit`,
                { answer, skipped }
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
