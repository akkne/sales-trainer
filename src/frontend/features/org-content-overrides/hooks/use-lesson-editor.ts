"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    AdminExercise,
    AdminLessonWithTopic,
    LessonAccuracySeries,
    LessonVersion,
    LessonVersionSummary,
    PublishLessonVersionResult,
    WriteExerciseRequest,
} from "../types/lesson-editor";

const LESSON_LIST_STALE_TIME_MILLISECONDS = 60_000;

export const LESSON_LIST_QUERY_KEY = ["org", "content", "lessons"] as const;

export const buildLessonVersionsQueryKey = (lessonId: string) =>
    ["org", "content", "lessons", lessonId, "versions"] as const;

export const buildLessonExercisesQueryKey = (lessonId: string) =>
    ["org", "content", "lessons", lessonId, "exercises"] as const;

export const buildLessonAccuracyQueryKey = (lessonId: string) =>
    ["org", "content", "lessons", lessonId, "accuracy"] as const;

/**
 * The whole visible library — the organization's own rows and the global ones, as the query filter
 * admits both.
 *
 * There is no `GET /admin/lessons/{id}`, so the editor's header (title, topic) comes from finding
 * the row in this list. The endpoint does not paginate, so one read is the whole answer.
 */
export function useAdminLessons() {
    return useQuery<AdminLessonWithTopic[]>({
        queryKey: LESSON_LIST_QUERY_KEY,
        queryFn: () => apiClient.get<AdminLessonWithTopic[]>("/admin/lessons"),
        staleTime: LESSON_LIST_STALE_TIME_MILLISECONDS,
    });
}

export function useLessonVersions(lessonId: string, enabled = true) {
    return useQuery<LessonVersionSummary[]>({
        queryKey: buildLessonVersionsQueryKey(lessonId),
        queryFn: () =>
            apiClient.get<LessonVersionSummary[]>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/versions`
            ),
        enabled,
    });
}

export function useLessonExercises(lessonId: string) {
    return useQuery<AdminExercise[]>({
        queryKey: buildLessonExercisesQueryKey(lessonId),
        queryFn: () =>
            apiClient.get<AdminExercise[]>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/exercises`
            ),
    });
}

/**
 * Counts only the calling organization's own attempts, which is why this chart is not drawn in the
 * platform panel: there it would compute «своё» for somebody who has none.
 */
export function useLessonAccuracy(lessonId: string) {
    return useQuery<LessonAccuracySeries>({
        queryKey: buildLessonAccuracyQueryKey(lessonId),
        queryFn: () =>
            apiClient.get<LessonAccuracySeries>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/accuracy`
            ),
    });
}

/** `PUT /admin/lessons/{id}` — title and ordering. Omitting the slug leaves it alone by contract. */
export function useUpdateLessonTitle(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<unknown, Error, { title: string; orderInTopic: number }>({
        mutationFn: (body) => apiClient.put(`/admin/lessons/${encodeURIComponent(lessonId)}`, body),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: LESSON_LIST_QUERY_KEY });
        },
    });
}

function invalidateLessonBody(queryClient: ReturnType<typeof useQueryClient>, lessonId: string) {
    void queryClient.invalidateQueries({ queryKey: buildLessonExercisesQueryKey(lessonId) });
    void queryClient.invalidateQueries({ queryKey: buildLessonVersionsQueryKey(lessonId) });
}

export function useCreateExercise(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<AdminExercise, Error, WriteExerciseRequest>({
        mutationFn: (body) =>
            apiClient.post<AdminExercise>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/exercises`,
                body
            ),
        onSuccess: () => invalidateLessonBody(queryClient, lessonId),
    });
}

export function useUpdateExercise(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<AdminExercise, Error, { exerciseId: string; body: WriteExerciseRequest }>({
        mutationFn: ({ exerciseId, body }) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${encodeURIComponent(exerciseId)}`, body),
        onSuccess: () => invalidateLessonBody(queryClient, lessonId),
    });
}

export function useDeleteExercise(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<void, Error, string>({
        mutationFn: (exerciseId) =>
            apiClient.delete<void>(`/admin/exercises/${encodeURIComponent(exerciseId)}`),
        onSuccess: () => invalidateLessonBody(queryClient, lessonId),
    });
}

export interface ExerciseOrderEntry {
    exerciseId: string;
    orderInLesson: number;
}

/**
 * Q-8 (`docs/NIGHT_AUDIT_QUESTIONS.md`). Persists a whole new exercise order in one request instead
 * of one `PUT /admin/exercises/{id}` per moved row, so a reorder cannot land half-applied and leave
 * two exercises claiming the same position. The route requires the full list of the lesson's
 * exercises, not just the moved ones — see its own doc comment for why a subset is unverifiable.
 */
export function useReorderExercises(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<AdminExercise[], Error, ExerciseOrderEntry[]>({
        mutationFn: (exercises) =>
            apiClient.put<AdminExercise[]>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/exercises/reorder`,
                { exercises }
            ),
        onSuccess: () => invalidateLessonBody(queryClient, lessonId),
    });
}

/**
 * Opens the one mutable version. Idempotent by contract — a lesson may have at most one draft, and
 * asking twice returns the same row.
 */
export function useEnsureLessonDraft(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<LessonVersion, Error, void>({
        mutationFn: () =>
            apiClient.post<LessonVersion>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/versions/draft`,
                {}
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: buildLessonVersionsQueryKey(lessonId) });
        },
    });
}

/**
 * Publishing freezes the draft. `isBreaking` is required of the caller and never inferred: a fixed
 * comma and a moved correct answer are the same diff (docs/TENANCY/CONTENT_MODEL.md §2.4).
 */
export function usePublishLessonVersion(lessonId: string) {
    const queryClient = useQueryClient();

    return useMutation<PublishLessonVersionResult, Error, boolean>({
        mutationFn: (isBreaking) =>
            apiClient.post<PublishLessonVersionResult>(
                `/admin/lessons/${encodeURIComponent(lessonId)}/versions/publish`,
                { isBreaking }
            ),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: buildLessonVersionsQueryKey(lessonId) });
            void queryClient.invalidateQueries({ queryKey: buildLessonAccuracyQueryKey(lessonId) });
        },
    });
}
