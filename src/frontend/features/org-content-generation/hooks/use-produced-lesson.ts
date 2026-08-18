"use client";

import { useMutation } from "@tanstack/react-query";
import { ApiError, apiClient } from "@/shared/api/api-client";

/** `AdminLessonWithTopicDto` — what `GET /admin/lessons` returns. It carries no `isArchived`. */
interface AdminLessonWithTopic {
    id: string;
    topicId: string;
    topicIconicName: string;
    topicTitle: string;
    title: string;
    orderInTopic: number;
}

const LESSON_NOT_FOUND_STATUS = 404;

/**
 * «Показать команде» — the way out of the archive a generated lesson lands in
 * (docs/CONTENT_PIPELINE.md §5: unreviewed model output stays out of the team's live tree until
 * somebody looks).
 *
 * It takes **two** requests, and that is a gap in the backend rather than a choice here.
 * `PUT /admin/lessons/{id}` is a whole-object update whose `title` and `orderInTopic` are required,
 * and sending either one wrong would rename the lesson or move it in its topic. There is no
 * `GET /admin/lessons/{id}` to read the current values from, so the only honest source is the list
 * route, filtered client-side. The same absence is why this screen **cannot show whether the lesson
 * is already visible**: the list DTO does not carry `isArchived`, so pressing the button is the
 * only way to know, and the action is idempotent so pressing it twice costs nothing.
 */
export function useUnarchiveProducedLesson() {
    return useMutation<void, Error, string>({
        mutationFn: async (lessonId: string) => {
            const lessons = await apiClient.get<AdminLessonWithTopic[]>("/admin/lessons");
            const producedLesson = lessons.find((lesson) => lesson.id === lessonId);

            if (!producedLesson) {
                throw new ApiError(LESSON_NOT_FOUND_STATUS, {
                    message: "Урок не найден среди уроков организации.",
                });
            }

            await apiClient.put<unknown>(`/admin/lessons/${encodeURIComponent(lessonId)}`, {
                title: producedLesson.title,
                orderInTopic: producedLesson.orderInTopic,
                isArchived: false,
            });
        },
    });
}
