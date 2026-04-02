"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";
import { clientLogger } from "@/lib/clientLogger";

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
        onSuccess: (data) => {
            clientLogger.info("Skill created", { skillId: data.id, slug: data.slug });
            qc.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create skill", { slug: variables.slug, error: (error as Error).message });
        },
    });
}

export function useUpdateSkill(id: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminSkill, "id">) =>
            apiClient.put<AdminSkill>(`/admin/skills/${id}`, body),
        onSuccess: (data) => {
            clientLogger.info("Skill updated", { skillId: data.id, slug: data.slug });
            qc.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update skill", { skillId: id, error: (error as Error).message });
        },
    });
}

export function useDeleteSkill() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/skills/${id}`),
        onSuccess: (_, id) => {
            clientLogger.warn("Skill deleted", { skillId: id });
            qc.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error, id) => {
            clientLogger.error("Failed to delete skill", { skillId: id, error: (error as Error).message });
        },
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
        onSuccess: (data) => {
            clientLogger.info("Lesson created", { lessonId: data.id, skillId, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create lesson", { skillId, title: variables.title, error: (error as Error).message });
        },
    });
}

export function useUpdateLesson(skillId: string, lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "skillId">) =>
            apiClient.put<AdminLesson>(`/admin/lessons/${lessonId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Lesson updated", { lessonId: data.id, skillId, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update lesson", { lessonId, skillId, error: (error as Error).message });
        },
    });
}

export function useDeleteLesson(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (lessonId: string) =>
            apiClient.delete<void>(`/admin/lessons/${lessonId}`),
        onSuccess: (_, lessonId) => {
            clientLogger.warn("Lesson deleted", { lessonId, skillId });
            qc.invalidateQueries({ queryKey: ["admin", "lessons", skillId] });
        },
        onError: (error, lessonId) => {
            clientLogger.error("Failed to delete lesson", { lessonId, skillId, error: (error as Error).message });
        },
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
        onSuccess: (data) => {
            clientLogger.info("Exercise created", { exerciseId: data.id, lessonId, type: data.type });
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create exercise", { lessonId, type: variables.type, error: (error as Error).message });
        },
    });
}

export function useUpdateExercise(lessonId: string, exerciseId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminExercise, "id" | "lessonId">) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Exercise updated", { exerciseId: data.id, lessonId, type: data.type });
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update exercise", { exerciseId, lessonId, error: (error as Error).message });
        },
    });
}

export function useDeleteExercise(lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (exerciseId: string) =>
            apiClient.delete<void>(`/admin/exercises/${exerciseId}`),
        onSuccess: (_, exerciseId) => {
            clientLogger.warn("Exercise deleted", { exerciseId, lessonId });
            qc.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error, exerciseId) => {
            clientLogger.error("Failed to delete exercise", { exerciseId, lessonId, error: (error as Error).message });
        },
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
        onSuccess: (data) => {
            clientLogger.info("Reference material created", { materialId: data.id, skillId, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create reference material", { skillId, title: variables.title, error: (error as Error).message });
        },
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
        onSuccess: (data) => {
            clientLogger.info("Reference material updated", { materialId: data.id, skillId, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update reference material", { materialId, skillId, error: (error as Error).message });
        },
    });
}

export function useDeleteReference(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (materialId: string) =>
            apiClient.delete<void>(`/admin/reference/${materialId}`),
        onSuccess: (_, materialId) => {
            clientLogger.warn("Reference material deleted", { materialId, skillId });
            qc.invalidateQueries({ queryKey: ["admin", "reference", skillId] });
        },
        onError: (error, materialId) => {
            clientLogger.error("Failed to delete reference material", { materialId, skillId, error: (error as Error).message });
        },
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
        onSuccess: (data, variables) => {
            clientLogger.info("User role changed", { userId: variables.id, newRole: variables.role, email: data.email });
            qc.invalidateQueries({ queryKey: ["admin", "users"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to change user role", { userId: variables.id, role: variables.role, error: (error as Error).message });
        },
    });
}
