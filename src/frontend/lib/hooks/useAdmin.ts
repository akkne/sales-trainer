"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

// --- Types ---

export interface AdminSkill {
    id: string;
    title: string;
    slug: string;
    iconName: string;
    sortOrder: number;
    prerequisiteSkillId: string | null;
    applicableSalesTypes: string[];
}

export interface AdminLesson {
    id: string;
    skillId: string;
    title: string;
    sortOrder: number;
    difficultyLevel: number;
    xpReward: number;
}

export interface AdminExercise {
    id: string;
    lessonId: string;
    type: string;
    sortOrder: number;
    content: Record<string, unknown>;
}

export interface AdminReferenceMaterial {
    id: string;
    skillId: string;
    title: string;
    markdownContent: string;
    sortOrder: number;
}

export interface AdminUser {
    id: string;
    email: string;
    displayName: string;
    role: string;
    createdAt: string;
}

// --- Skills ---

export function useAdminSkills() {
    return useQuery({
        queryKey: ["admin", "skills"],
        queryFn: () => apiClient.get<AdminSkill[]>("/admin/skills"),
    });
}

export function useCreateSkill() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminSkill, "id">) =>
            apiClient.post<AdminSkill>("/admin/skills", body),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["admin", "skills"] }),
    });
}

export function useUpdateSkill(id: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminSkill, "id">) =>
            apiClient.put<AdminSkill>(`/admin/skills/${id}`, body),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["admin", "skills"] }),
    });
}

export function useDeleteSkill() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/skills/${id}`),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["admin", "skills"] }),
    });
}

// --- Lessons ---

export function useAdminLessons(skillId: string) {
    return useQuery({
        queryKey: ["admin", "lessons", skillId],
        queryFn: () => apiClient.get<AdminLesson[]>(`/admin/skills/${skillId}/lessons`),
        enabled: !!skillId,
    });
}

export function useCreateLesson(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "skillId">) =>
            apiClient.post<AdminLesson>(`/admin/skills/${skillId}/lessons`, body),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] }),
    });
}

export function useUpdateLesson(skillId: string, lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "skillId">) =>
            apiClient.put<AdminLesson>(`/admin/lessons/${lessonId}`, body),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] }),
    });
}

export function useDeleteLesson(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (lessonId: string) =>
            apiClient.delete<void>(`/admin/lessons/${lessonId}`),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] }),
    });
}

// --- Exercises ---

export function useAdminExercises(lessonId: string) {
    return useQuery({
        queryKey: ["admin", "exercises", lessonId],
        queryFn: () =>
            apiClient.get<AdminExercise[]>(`/admin/lessons/${lessonId}/exercises`),
        enabled: !!lessonId,
    });
}

export function useCreateExercise(lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminExercise, "id" | "lessonId">) =>
            apiClient.post<AdminExercise>(`/admin/lessons/${lessonId}/exercises`, body),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] }),
    });
}

export function useUpdateExercise(lessonId: string, exerciseId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminExercise, "id" | "lessonId">) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] }),
    });
}

export function useDeleteExercise(lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (exerciseId: string) =>
            apiClient.delete<void>(`/admin/exercises/${exerciseId}`),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] }),
    });
}

// --- Reference ---

export function useAdminReference(skillId: string) {
    return useQuery({
        queryKey: ["admin", "reference", skillId],
        queryFn: () =>
            apiClient.get<AdminReferenceMaterial[]>(
                `/admin/skills/${skillId}/reference`
            ),
        enabled: !!skillId,
    });
}

export function useCreateReference(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminReferenceMaterial, "id" | "skillId">) =>
            apiClient.post<AdminReferenceMaterial>(
                `/admin/skills/${skillId}/reference`,
                body
            ),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] }),
    });
}

export function useUpdateReference(skillId: string, materialId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminReferenceMaterial, "id" | "skillId">) =>
            apiClient.put<AdminReferenceMaterial>(
                `/admin/reference/${materialId}`,
                body
            ),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] }),
    });
}

export function useDeleteReference(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (materialId: string) =>
            apiClient.delete<void>(`/admin/reference/${materialId}`),
        onSuccess: () =>
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] }),
    });
}

// --- Users ---

export function useAdminUsers() {
    return useQuery({
        queryKey: ["admin", "users"],
        queryFn: () => apiClient.get<AdminUser[]>("/admin/users"),
    });
}

export function useChangeUserRole() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: ({ id, role }: { id: string; role: string }) =>
            apiClient.put<AdminUser>(`/admin/users/${id}/role`, { role }),
        onSuccess: () => qc.invalidateQueries({ queryKey: ["admin", "users"] }),
    });
}
