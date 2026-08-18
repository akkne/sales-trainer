"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import type {
    Assignment,
    AssignmentDashboard,
    AssignmentProgressRecord,
    AssignmentReminderResult,
    AssignmentReminderScope,
    AssignmentSummary,
    CreateAssignmentRequest,
    UpdateAssignmentRequest,
} from "@/features/org-assignments/types/assignment";

const ASSIGNMENT_STALE_TIME_MILLISECONDS = 30_000;

export const ASSIGNMENT_LIST_QUERY_KEY = ["org", "assignments"] as const;

/**
 * `GET /admin/assignments`, optionally narrowed to one status.
 *
 * The endpoint does not paginate — there is no cursor on the server and building an infinite scroll
 * against it would be inventing a contract — so the whole array arrives and the screen filters by
 * chips over fixed values.
 */
export function useOrganizationAssignments(status?: string) {
    const queryPath =
        status === undefined ? "/admin/assignments" : `/admin/assignments?status=${status}`;

    return useQuery<AssignmentSummary[]>({
        queryKey: [...ASSIGNMENT_LIST_QUERY_KEY, status ?? "all"],
        queryFn: () => apiClient.get<AssignmentSummary[]>(queryPath),
        staleTime: ASSIGNMENT_STALE_TIME_MILLISECONDS,
    });
}

/** The full assignment, jsonb columns included — what the edit form needs and the dashboard omits. */
export function useOrganizationAssignment(assignmentId: string) {
    return useQuery<Assignment>({
        queryKey: ["org", "assignment", assignmentId],
        queryFn: () => apiClient.get<Assignment>(`/admin/assignments/${assignmentId}`),
        enabled: assignmentId.length > 0,
        retry: false,
    });
}

/** Funnel, named rows and the whole repeat series in one read (40.25). */
export function useAssignmentDashboard(assignmentId: string) {
    return useQuery<AssignmentDashboard>({
        queryKey: ["org", "assignment", assignmentId, "dashboard"],
        queryFn: () => apiClient.get<AssignmentDashboard>(`/admin/assignments/${assignmentId}/dashboard`),
        enabled: assignmentId.length > 0,
        staleTime: ASSIGNMENT_STALE_TIME_MILLISECONDS,
        retry: false,
    });
}

/**
 * The raw, name-free rows. Fetched only when the caller asks for them, because its whole value is
 * being the one read on this screen that cannot be taken down by identity-service.
 */
export function useAssignmentRawProgress(assignmentId: string, isEnabled: boolean) {
    return useQuery<AssignmentProgressRecord[]>({
        queryKey: ["org", "assignment", assignmentId, "progress"],
        queryFn: () =>
            apiClient.get<AssignmentProgressRecord[]>(`/admin/assignments/${assignmentId}/progress`),
        enabled: isEnabled && assignmentId.length > 0,
        retry: false,
    });
}

function useAssignmentCacheInvalidation() {
    const queryClient = useQueryClient();

    return (assignmentId?: string) => {
        void queryClient.invalidateQueries({ queryKey: ASSIGNMENT_LIST_QUERY_KEY });
        void queryClient.invalidateQueries({ queryKey: ["org", "nav-badges", "assignments"] });
        if (assignmentId) {
            void queryClient.invalidateQueries({ queryKey: ["org", "assignment", assignmentId] });
        }
    };
}

export function useCreateAssignment() {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<Assignment, unknown, CreateAssignmentRequest>({
        mutationFn: (request) => apiClient.post<Assignment>("/admin/assignments", request),
        onSuccess: (assignment) => invalidateAssignments(assignment.id),
    });
}

export function useUpdateAssignment(assignmentId: string) {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<Assignment, unknown, UpdateAssignmentRequest>({
        mutationFn: (request) =>
            apiClient.put<Assignment>(`/admin/assignments/${assignmentId}`, request),
        onSuccess: () => invalidateAssignments(assignmentId),
    });
}

/** Issuing resolves the audience against the live roster, so this is the call that can answer 503. */
export function useActivateAssignment() {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<Assignment, unknown, string>({
        mutationFn: (assignmentId) =>
            apiClient.post<Assignment>(`/admin/assignments/${assignmentId}/activate`, {}),
        onSuccess: (assignment) => invalidateAssignments(assignment.id),
    });
}

export function useCloseAssignment(assignmentId: string) {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<Assignment, unknown, void>({
        mutationFn: () => apiClient.post<Assignment>(`/admin/assignments/${assignmentId}/close`, {}),
        onSuccess: () => invalidateAssignments(assignmentId),
    });
}

export function useDeleteAssignment() {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<void, unknown, string>({
        mutationFn: (assignmentId) => apiClient.delete<void>(`/admin/assignments/${assignmentId}`),
        onSuccess: () => invalidateAssignments(),
    });
}

export function useRemindAssignment(assignmentId: string) {
    const invalidateAssignments = useAssignmentCacheInvalidation();

    return useMutation<AssignmentReminderResult, unknown, AssignmentReminderScope>({
        mutationFn: (scope) =>
            apiClient.post<AssignmentReminderResult>(
                `/admin/assignments/${assignmentId}/remind?scope=${scope}`,
                {}
            ),
        onSuccess: () => invalidateAssignments(assignmentId),
    });
}
