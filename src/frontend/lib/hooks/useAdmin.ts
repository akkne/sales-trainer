"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";
import { clientLogger } from "@/lib/clientLogger";

// --- Types ---

export interface AdminSkill {
    id: string;
    iconicName: string;
    title: string;
    description: string | null;
    orderInTree: number;
}

export interface AdminTopic {
    id: string;
    skillId: string;
    iconicName: string;
    title: string;
    orderInSkill: number;
}

export interface AdminTopicWithSkill extends AdminTopic {
    skillIconicName: string;
    skillTitle: string;
}

export interface AdminLesson {
    id: string;
    topicId: string;
    title: string;
    orderInTopic: number;
}

export interface AdminLessonWithTopic extends AdminLesson {
    topicIconicName: string;
    topicTitle: string;
}

export interface AdminExercise {
    id: string;
    lessonId: string;
    type: string;
    orderInLesson: number;
    content: Record<string, unknown>;
    customAiPrompt: string | null;
}

export interface AdminReferenceMaterial {
    id: string;
    skillId: string;
    skillTitle: string;
    title: string;
    markdownContent: string;
    sortOrder: number;
    category: string | null;
    tags: string[];
}

export interface CreateReferenceMaterialBody {
    title: string;
    markdownContent: string;
    sortOrder: number;
    category?: string | null;
    tags?: string | null;
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
            clientLogger.info("Skill created", { skillId: data.id, iconicName: data.iconicName });
            qc.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create skill", { iconicName: variables.iconicName, error: (error as Error).message });
        },
    });
}

export function useUpdateSkill(id: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Partial<Omit<AdminSkill, "id">>) =>
            apiClient.put<AdminSkill>(`/admin/skills/${id}`, body),
        onSuccess: (data) => {
            clientLogger.info("Skill updated", { skillId: data.id, iconicName: data.iconicName });
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

// --- Topics ---

export function useAdminAllTopics() {
    return useQuery({
        queryKey: ["admin", "topics"],
        queryFn: () => apiClient.get<AdminTopicWithSkill[]>("/admin/topics"),
    });
}

export function useAdminTopics(skillIconicName: string) {
    return useQuery({
        queryKey: ["admin", "topics", skillIconicName],
        queryFn: () => apiClient.get<AdminTopic[]>(`/admin/skills/${skillIconicName}/topics`),
        enabled: !!skillIconicName,
    });
}

export function useCreateTopic(skillIconicName: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminTopic, "id" | "skillId">) =>
            apiClient.post<AdminTopic>(`/admin/skills/${skillIconicName}/topics`, body),
        onSuccess: (data) => {
            clientLogger.info("Topic created", { topicId: data.id, skillIconicName, iconicName: data.iconicName });
            qc.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create topic", { skillIconicName, iconicName: variables.iconicName, error: (error as Error).message });
        },
    });
}

export function useUpdateTopic(topicId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Partial<Omit<AdminTopic, "id" | "skillId">>) =>
            apiClient.put<AdminTopic>(`/admin/topics/${topicId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Topic updated", { topicId: data.id, iconicName: data.iconicName });
            qc.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update topic", { topicId, error: (error as Error).message });
        },
    });
}

export function useDeleteTopic(skillId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (topicId: string) =>
            apiClient.delete<void>(`/admin/topics/${topicId}`),
        onSuccess: (_, topicId) => {
            clientLogger.warn("Topic deleted", { topicId, skillId });
            qc.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error, topicId) => {
            clientLogger.error("Failed to delete topic", { topicId, skillId, error: (error as Error).message });
        },
    });
}

// --- Lessons ---

export function useAdminAllLessons() {
    return useQuery({
        queryKey: ["admin", "lessons"],
        queryFn: () => apiClient.get<AdminLessonWithTopic[]>("/admin/lessons"),
    });
}

export function useAdminLessons(topicIconicName: string) {
    return useQuery({
        queryKey: ["admin", "lessons", topicIconicName],
        queryFn: () => apiClient.get<AdminLesson[]>(`/admin/topics/${topicIconicName}/lessons`),
        enabled: !!topicIconicName,
    });
}

export function useCreateLesson(topicIconicName: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "topicId">) =>
            apiClient.post<AdminLesson>(`/admin/topics/${topicIconicName}/lessons`, body),
        onSuccess: (data) => {
            clientLogger.info("Lesson created", { lessonId: data.id, topicIconicName, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create lesson", { topicIconicName, title: variables.title, error: (error as Error).message });
        },
    });
}

export function useUpdateLesson(lessonId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "topicId">) =>
            apiClient.put<AdminLesson>(`/admin/lessons/${lessonId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Lesson updated", { lessonId: data.id, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update lesson", { lessonId, error: (error as Error).message });
        },
    });
}

export function useDeleteLesson(topicId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (lessonId: string) =>
            apiClient.delete<void>(`/admin/lessons/${lessonId}`),
        onSuccess: (_, lessonId) => {
            clientLogger.warn("Lesson deleted", { lessonId, topicId });
            qc.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error, lessonId) => {
            clientLogger.error("Failed to delete lesson", { lessonId, topicId, error: (error as Error).message });
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

export function useAdminReferenceAll(filters?: { skillId?: string; category?: string; search?: string }) {
    const params = new URLSearchParams();
    if (filters?.skillId) params.set("skillId", filters.skillId);
    if (filters?.category) params.set("category", filters.category);
    if (filters?.search) params.set("search", filters.search);
    const queryString = params.toString();
    return useQuery({
        queryKey: ["admin", "reference", "all", filters],
        queryFn: () =>
            apiClient.get<AdminReferenceMaterial[]>(
                `/admin/reference${queryString ? `?${queryString}` : ""}`
            ),
    });
}

export function useAdminReferenceCategories() {
    return useQuery({
        queryKey: ["admin", "reference", "categories"],
        queryFn: () => apiClient.get<string[]>("/admin/reference/categories"),
    });
}

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
        mutationFn: (body: CreateReferenceMaterialBody) =>
            apiClient.post<AdminReferenceMaterial>(
                `/admin/skills/${skillId}/reference`,
                body
            ),
        onSuccess: (data) => {
            clientLogger.info("Reference material created", { materialId: data.id, skillId, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "reference"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create reference material", { skillId, title: variables.title, error: (error as Error).message });
        },
    });
}

export function useUpdateReference(materialId: string) {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (body: CreateReferenceMaterialBody) =>
            apiClient.put<AdminReferenceMaterial>(
                `/admin/reference/${materialId}`,
                body
            ),
        onSuccess: (data) => {
            clientLogger.info("Reference material updated", { materialId: data.id, title: data.title });
            qc.invalidateQueries({ queryKey: ["admin", "reference"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update reference material", { materialId, error: (error as Error).message });
        },
    });
}

export function useDeleteReference() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (materialId: string) =>
            apiClient.delete<void>(`/admin/reference/${materialId}`),
        onSuccess: (_, materialId) => {
            clientLogger.warn("Reference material deleted", { materialId });
            qc.invalidateQueries({ queryKey: ["admin", "reference"] });
        },
        onError: (error, materialId) => {
            clientLogger.error("Failed to delete reference material", { materialId, error: (error as Error).message });
        },
    });
}

// --- Seeder ---

export interface SkillsImportResult {
    skillsCreated: number;
    skillsUpdated: number;
    errors: string[];
}

export interface TopicsImportResult {
    topicsCreated: number;
    topicsUpdated: number;
    errors: string[];
}

export interface LessonsImportResult {
    lessonsCreated: number;
    lessonsUpdated: number;
    exercisesCreated: number;
    exercisesUpdated: number;
    errors: string[];
}

export function useImportSkills() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (file: File) => {
            const formData = new FormData();
            formData.append("file", file);
            return apiClient.postFile<SkillsImportResult>("/admin/seeder/skills", formData);
        },
        onSuccess: (data) => {
            clientLogger.info("Skills seeder import complete", {
                skillsCreated: data.skillsCreated,
                skillsUpdated: data.skillsUpdated,
                errors: data.errors.length,
            });
            qc.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error) => {
            clientLogger.error("Skills seeder import failed", { error: (error as Error).message });
        },
    });
}

export function useImportTopics() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (file: File) => {
            const formData = new FormData();
            formData.append("file", file);
            return apiClient.postFile<TopicsImportResult>("/admin/seeder/topics", formData);
        },
        onSuccess: (data) => {
            clientLogger.info("Topics seeder import complete", {
                topicsCreated: data.topicsCreated,
                topicsUpdated: data.topicsUpdated,
                errors: data.errors.length,
            });
            qc.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error) => {
            clientLogger.error("Topics seeder import failed", { error: (error as Error).message });
        },
    });
}

export function useImportLessons() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: (file: File) => {
            const formData = new FormData();
            formData.append("file", file);
            return apiClient.postFile<LessonsImportResult>("/admin/seeder/lessons", formData);
        },
        onSuccess: (data) => {
            clientLogger.info("Lessons seeder import complete", {
                lessonsCreated: data.lessonsCreated,
                exercisesCreated: data.exercisesCreated,
                errors: data.errors.length,
            });
            qc.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error) => {
            clientLogger.error("Lessons seeder import failed", { error: (error as Error).message });
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

// --- Exercise Type Prompts ---

export interface ExerciseTypePrompt {
    id: string;
    exerciseType: string;
    systemPrompt: string;
    updatedAt: string;
}

export function useExerciseTypePrompts() {
    return useQuery({
        queryKey: ["admin", "exercise-type-prompts"],
        queryFn: () => apiClient.get<ExerciseTypePrompt[]>("/admin/exercise-type-prompts"),
    });
}

export function useExerciseTypePrompt(exerciseType: string) {
    return useQuery({
        queryKey: ["admin", "exercise-type-prompts", exerciseType],
        queryFn: () => apiClient.get<ExerciseTypePrompt>(`/admin/exercise-type-prompts/${exerciseType}`),
        enabled: !!exerciseType,
    });
}

export function useUpdateExerciseTypePrompt() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: ({ exerciseType, systemPrompt }: { exerciseType: string; systemPrompt: string }) =>
            apiClient.put<ExerciseTypePrompt>(`/admin/exercise-type-prompts/${exerciseType}`, { systemPrompt }),
        onSuccess: (data) => {
            clientLogger.info("Exercise type prompt updated", { exerciseType: data.exerciseType });
            qc.invalidateQueries({ queryKey: ["admin", "exercise-type-prompts"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to update exercise type prompt", { exerciseType: variables.exerciseType, error: (error as Error).message });
        },
    });
}
