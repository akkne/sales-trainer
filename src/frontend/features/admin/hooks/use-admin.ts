"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";

// --- Types ---

export interface AdminSkill {
    id: string;
    iconicName: string;
    title: string;
    description: string | null;
    orderInTree: number;
    stage: string;
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
    isEmailVerified: boolean;
    authProvider: string;
    hasCustomAvatar: boolean;
    avatarUrl: string;
}

export interface AdminUserDetail extends AdminUser {
    currentStreakDayCount: number;
    longestStreakDayCount: number;
    totalXpAmount: number;
    completedSkillCount: number;
    totalSkillCount: number;
    averageExerciseScore: number;
    persona: string | null;
}

// --- Skills ---

export function useAdminSkills() {
    return useQuery({
        queryKey: ["admin", "skills"],
        queryFn: () => apiClient.get<AdminSkill[]>("/admin/skills"),
    });
}

export function useCreateSkill() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminSkill, "id">) =>
            apiClient.post<AdminSkill>("/admin/skills", body),
        onSuccess: (data) => {
            clientLogger.info("Skill created", { skillId: data.id, iconicName: data.iconicName });
            queryClient.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create skill", { iconicName: variables.iconicName, error: (error as Error).message });
        },
    });
}

export function useUpdateSkill(id: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Partial<Omit<AdminSkill, "id">>) =>
            apiClient.put<AdminSkill>(`/admin/skills/${id}`, body),
        onSuccess: (data) => {
            clientLogger.info("Skill updated", { skillId: data.id, iconicName: data.iconicName });
            queryClient.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update skill", { skillId: id, error: (error as Error).message });
        },
    });
}

export function useDeleteSkill() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/skills/${id}`),
        onSuccess: (_, id) => {
            clientLogger.warn("Skill deleted", { skillId: id });
            queryClient.invalidateQueries({ queryKey: ["admin", "skills"] });
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
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminTopic, "id" | "skillId">) =>
            apiClient.post<AdminTopic>(`/admin/skills/${skillIconicName}/topics`, body),
        onSuccess: (data) => {
            clientLogger.info("Topic created", { topicId: data.id, skillIconicName, iconicName: data.iconicName });
            queryClient.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create topic", { skillIconicName, iconicName: variables.iconicName, error: (error as Error).message });
        },
    });
}

export function useUpdateTopic(topicId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Partial<Omit<AdminTopic, "id" | "skillId">>) =>
            apiClient.put<AdminTopic>(`/admin/topics/${topicId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Topic updated", { topicId: data.id, iconicName: data.iconicName });
            queryClient.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update topic", { topicId, error: (error as Error).message });
        },
    });
}

export function useDeleteTopic(skillId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (topicId: string) =>
            apiClient.delete<void>(`/admin/topics/${topicId}`),
        onSuccess: (_, topicId) => {
            clientLogger.warn("Topic deleted", { topicId, skillId });
            queryClient.invalidateQueries({ queryKey: ["admin", "topics"] });
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
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "topicId">) =>
            apiClient.post<AdminLesson>(`/admin/topics/${topicIconicName}/lessons`, body),
        onSuccess: (data) => {
            clientLogger.info("Lesson created", { lessonId: data.id, topicIconicName, title: data.title });
            queryClient.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create lesson", { topicIconicName, title: variables.title, error: (error as Error).message });
        },
    });
}

export function useUpdateLesson(lessonId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminLesson, "id" | "topicId">) =>
            apiClient.put<AdminLesson>(`/admin/lessons/${lessonId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Lesson updated", { lessonId: data.id, title: data.title });
            queryClient.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update lesson", { lessonId, error: (error as Error).message });
        },
    });
}

export function useDeleteLesson(topicId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (lessonId: string) =>
            apiClient.delete<void>(`/admin/lessons/${lessonId}`),
        onSuccess: (_, lessonId) => {
            clientLogger.warn("Lesson deleted", { lessonId, topicId });
            queryClient.invalidateQueries({ queryKey: ["admin", "lessons"] });
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
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminExercise, "id" | "lessonId">) =>
            apiClient.post<AdminExercise>(`/admin/lessons/${lessonId}/exercises`, body),
        onSuccess: (data) => {
            clientLogger.info("Exercise created", { exerciseId: data.id, lessonId, type: data.type });
            queryClient.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create exercise", { lessonId, type: variables.type, error: (error as Error).message });
        },
    });
}

export function useUpdateExercise(lessonId: string, exerciseId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: Omit<AdminExercise, "id" | "lessonId">) =>
            apiClient.put<AdminExercise>(`/admin/exercises/${exerciseId}`, body),
        onSuccess: (data) => {
            clientLogger.info("Exercise updated", { exerciseId: data.id, lessonId, type: data.type });
            queryClient.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update exercise", { exerciseId, lessonId, error: (error as Error).message });
        },
    });
}

export function useDeleteExercise(lessonId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (exerciseId: string) =>
            apiClient.delete<void>(`/admin/exercises/${exerciseId}`),
        onSuccess: (_, exerciseId) => {
            clientLogger.warn("Exercise deleted", { exerciseId, lessonId });
            queryClient.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error, exerciseId) => {
            clientLogger.error("Failed to delete exercise", { exerciseId, lessonId, error: (error as Error).message });
        },
    });
}

export interface ExercisesImportResult {
    exercisesCreated: number;
    exercisesUpdated: number;
    errors: string[];
}

export function useImportExercises(lessonId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (items: Omit<AdminExercise, "id" | "lessonId">[]) =>
            apiClient.post<ExercisesImportResult>(`/admin/lessons/${lessonId}/exercises/import`, items),
        onSuccess: (data) => {
            clientLogger.info("Exercises import complete", {
                lessonId,
                created: data.exercisesCreated,
                updated: data.exercisesUpdated,
                errors: data.errors.length,
            });
            queryClient.invalidateQueries({ queryKey: ["admin", "exercises", lessonId] });
        },
        onError: (error) => {
            clientLogger.error("Exercises import failed", { lessonId, error: (error as Error).message });
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
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: CreateReferenceMaterialBody) =>
            apiClient.post<AdminReferenceMaterial>(
                `/admin/skills/${skillId}/reference`,
                body
            ),
        onSuccess: (data) => {
            clientLogger.info("Reference material created", { materialId: data.id, skillId, title: data.title });
            queryClient.invalidateQueries({ queryKey: ["admin", "reference"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create reference material", { skillId, title: variables.title, error: (error as Error).message });
        },
    });
}

export function useUpdateReference(materialId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: CreateReferenceMaterialBody) =>
            apiClient.put<AdminReferenceMaterial>(
                `/admin/reference/${materialId}`,
                body
            ),
        onSuccess: (data) => {
            clientLogger.info("Reference material updated", { materialId: data.id, title: data.title });
            queryClient.invalidateQueries({ queryKey: ["admin", "reference"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update reference material", { materialId, error: (error as Error).message });
        },
    });
}

export function useDeleteReference() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (materialId: string) =>
            apiClient.delete<void>(`/admin/reference/${materialId}`),
        onSuccess: (_, materialId) => {
            clientLogger.warn("Reference material deleted", { materialId });
            queryClient.invalidateQueries({ queryKey: ["admin", "reference"] });
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
    const queryClient = useQueryClient();
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
            queryClient.invalidateQueries({ queryKey: ["admin", "skills"] });
        },
        onError: (error) => {
            clientLogger.error("Skills seeder import failed", { error: (error as Error).message });
        },
    });
}

export function useImportTopics() {
    const queryClient = useQueryClient();
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
            queryClient.invalidateQueries({ queryKey: ["admin", "topics"] });
        },
        onError: (error) => {
            clientLogger.error("Topics seeder import failed", { error: (error as Error).message });
        },
    });
}

export function useImportLessons() {
    const queryClient = useQueryClient();
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
            queryClient.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error) => {
            clientLogger.error("Lessons seeder import failed", { error: (error as Error).message });
        },
    });
}

export interface BundleImportResult {
    skillsCreated: number;
    skillsUpdated: number;
    topicsCreated: number;
    topicsUpdated: number;
    lessonsCreated: number;
    lessonsUpdated: number;
    exercisesCreated: number;
    exercisesUpdated: number;
    errors: string[];
}

/** Import an entire content tree (skills → topics → lessons → exercises) from one file. */
export function useImportBundle() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (file: File) => {
            const formData = new FormData();
            formData.append("file", file);
            return apiClient.postFile<BundleImportResult>("/admin/seeder/bundle", formData);
        },
        onSuccess: (data) => {
            clientLogger.info("Bundle seeder import complete", {
                skillsCreated: data.skillsCreated,
                topicsCreated: data.topicsCreated,
                lessonsCreated: data.lessonsCreated,
                exercisesCreated: data.exercisesCreated,
                errors: data.errors.length,
            });
            queryClient.invalidateQueries({ queryKey: ["admin", "skills"] });
            queryClient.invalidateQueries({ queryKey: ["admin", "topics"] });
            queryClient.invalidateQueries({ queryKey: ["admin", "lessons"] });
        },
        onError: (error) => {
            clientLogger.error("Bundle seeder import failed", { error: (error as Error).message });
        },
    });
}

// --- Techniques ---

export interface AdminTechniqueCoach {
    avatarSeed: string;
    name: string;
    role: string;
    quote: string;
    challenges: unknown;
}

export interface AdminTechnique {
    id: string;
    slug: string;
    name: string;
    summary: string;
    body: string;
    tags: string[];
    primarySkillId: string | null;
    primarySkillIconicName: string | null;
    primarySkillTitle: string | null;
    additionalSkillIds: string[];
    difficulty: number;
    difficultyName: string;
    sortOrder: number;
    createdAt: string;
    updatedAt: string;
    dialog: unknown;
    case: unknown;
    coach: AdminTechniqueCoach | null;
}

export interface AdminTechniqueWriteBody {
    slug: string;
    name: string;
    summary: string;
    body: string;
    tags: string[];
    primarySkillId: string | null;
    additionalSkillIds: string[];
    difficulty: number;
    sortOrder: number;
    dialog: unknown;
    case: unknown;
    coach: AdminTechniqueCoach | null;
}

export interface AdminTechniqueImportResult {
    createdCount: number;
    updatedCount: number;
    failedCount: number;
    errors: string[];
}

export function useAdminTechniques(filters?: { skill?: string; search?: string }) {
    const params = new URLSearchParams();
    if (filters?.skill) params.set("skill", filters.skill);
    if (filters?.search) params.set("search", filters.search);
    const qs = params.toString();
    return useQuery({
        queryKey: ["admin", "techniques", filters],
        queryFn: () =>
            apiClient.get<AdminTechnique[]>(
                `/admin/techniques${qs ? `?${qs}` : ""}`
            ),
    });
}

export function useCreateTechnique() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: AdminTechniqueWriteBody) =>
            apiClient.post<AdminTechnique>("/admin/techniques", body),
        onSuccess: (data) => {
            clientLogger.info("Technique created", { techniqueId: data.id, slug: data.slug });
            queryClient.invalidateQueries({ queryKey: ["admin", "techniques"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create technique", { slug: variables.slug, error: (error as Error).message });
        },
    });
}

export function useUpdateTechnique(id: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: AdminTechniqueWriteBody) =>
            apiClient.put<AdminTechnique>(`/admin/techniques/${id}`, body),
        onSuccess: (data) => {
            clientLogger.info("Technique updated", { techniqueId: data.id, slug: data.slug });
            queryClient.invalidateQueries({ queryKey: ["admin", "techniques"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update technique", { techniqueId: id, error: (error as Error).message });
        },
    });
}

export function useDeleteTechnique() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/techniques/${id}`),
        onSuccess: (_, id) => {
            clientLogger.warn("Technique deleted", { techniqueId: id });
            queryClient.invalidateQueries({ queryKey: ["admin", "techniques"] });
        },
        onError: (error, id) => {
            clientLogger.error("Failed to delete technique", { techniqueId: id, error: (error as Error).message });
        },
    });
}

export function useImportTechniques() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (items: AdminTechniqueWriteBody[]) =>
            apiClient.post<AdminTechniqueImportResult>("/admin/techniques/import", items),
        onSuccess: (data) => {
            clientLogger.info("Techniques import complete", {
                created: data.createdCount,
                updated: data.updatedCount,
                failed: data.failedCount,
            });
            queryClient.invalidateQueries({ queryKey: ["admin", "techniques"] });
        },
        onError: (error) => {
            clientLogger.error("Techniques import failed", { error: (error as Error).message });
        },
    });
}

// --- Daily Quotes ---

export interface AdminDailyQuote {
    id: string;
    date: string;
    text: string;
    author: string;
    createdAt: string;
    updatedAt: string;
}

export interface AdminDailyQuoteWriteBody {
    date: string;
    text: string;
    author: string | null;
}

export function useAdminDailyQuotes(filters?: { from?: string; to?: string }) {
    const params = new URLSearchParams();
    if (filters?.from) params.set("from", filters.from);
    if (filters?.to) params.set("to", filters.to);
    const queryString = params.toString();
    return useQuery({
        queryKey: ["admin", "daily-quotes", filters],
        queryFn: () =>
            apiClient.get<AdminDailyQuote[]>(
                `/admin/daily-quotes${queryString ? `?${queryString}` : ""}`
            ),
    });
}

export function useCreateDailyQuote() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: AdminDailyQuoteWriteBody) =>
            apiClient.post<AdminDailyQuote>("/admin/daily-quotes", body),
        onSuccess: (data) => {
            clientLogger.info("Daily quote created", { quoteId: data.id, date: data.date });
            queryClient.invalidateQueries({ queryKey: ["admin", "daily-quotes"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create daily quote", { date: variables.date, error: (error as Error).message });
        },
    });
}

export function useUpdateDailyQuote() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, body }: { id: string; body: AdminDailyQuoteWriteBody }) =>
            apiClient.put<AdminDailyQuote>(`/admin/daily-quotes/${id}`, body),
        onSuccess: (data) => {
            clientLogger.info("Daily quote updated", { quoteId: data.id, date: data.date });
            queryClient.invalidateQueries({ queryKey: ["admin", "daily-quotes"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to update daily quote", { quoteId: variables.id, error: (error as Error).message });
        },
    });
}

export function useDeleteDailyQuote() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/daily-quotes/${id}`),
        onSuccess: (_, id) => {
            clientLogger.warn("Daily quote deleted", { quoteId: id });
            queryClient.invalidateQueries({ queryKey: ["admin", "daily-quotes"] });
        },
        onError: (error, id) => {
            clientLogger.error("Failed to delete daily quote", { quoteId: id, error: (error as Error).message });
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

export function useAdminUser(id: string | null) {
    return useQuery({
        queryKey: ["admin", "users", "detail", id],
        queryFn: () => apiClient.get<AdminUserDetail>(`/admin/users/${id}`),
        enabled: !!id,
    });
}

export function useChangeUserRole() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, role }: { id: string; role: string }) =>
            apiClient.put<AdminUser>(`/admin/users/${id}/role`, { role }),
        onSuccess: (data, variables) => {
            clientLogger.info("User role changed", { userId: variables.id, newRole: variables.role, email: data.email });
            queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to change user role", { userId: variables.id, role: variables.role, error: (error as Error).message });
        },
    });
}

export function useUpdateUser() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, displayName }: { id: string; displayName: string }) =>
            apiClient.put<AdminUser>(`/admin/users/${id}`, { displayName }),
        onSuccess: (data, variables) => {
            clientLogger.info("User display name changed", { userId: variables.id, displayName: data.displayName });
            queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to update user", { userId: variables.id, error: (error as Error).message });
        },
    });
}

export function useDeleteUserAvatar() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/admin/users/${id}/avatar`),
        onSuccess: (_, id) => {
            clientLogger.warn("User avatar reset by admin", { userId: id });
            queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
        },
        onError: (error, id) => {
            clientLogger.error("Failed to reset user avatar", { userId: id, error: (error as Error).message });
        },
    });
}

// --- Leagues ---

export interface AdminLeagueListItem {
    id: string;
    tier: string;
    weekStartDate: string;
    weekEndDate: string;
    memberCount: number;
}

export interface AdminLeagueMember {
    membershipId: string;
    userId: string;
    displayName: string;
    email: string;
    weeklyXpAmount: number;
    rank: number;
    promotionOutcome: string | null;
}

export interface AdminLeagueDetail {
    id: string;
    tier: string;
    weekStartDate: string;
    weekEndDate: string;
    members: AdminLeagueMember[];
}

export interface AdminLeagueSettings {
    maximumLeagueParticipantCount: number;
    promotionZoneSize: number;
    demotionZoneSize: number;
}

export function useAdminLeagues(filters?: { weekStart?: string; tier?: string }) {
    const params = new URLSearchParams();
    if (filters?.weekStart) params.set("weekStart", filters.weekStart);
    if (filters?.tier) params.set("tier", filters.tier);
    const queryString = params.toString();
    return useQuery({
        queryKey: ["admin", "leagues", filters],
        queryFn: () =>
            apiClient.get<AdminLeagueListItem[]>(
                `/admin/leagues${queryString ? `?${queryString}` : ""}`
            ),
    });
}

export function useAdminLeagueWeeks() {
    return useQuery({
        queryKey: ["admin", "leagues", "weeks"],
        queryFn: () => apiClient.get<string[]>("/admin/leagues/weeks"),
    });
}

export function useAdminLeagueDetail(leagueId: string) {
    return useQuery({
        queryKey: ["admin", "leagues", "detail", leagueId],
        queryFn: () => apiClient.get<AdminLeagueDetail>(`/admin/leagues/${leagueId}`),
        enabled: !!leagueId,
    });
}

export function useAdminLeagueSettings() {
    return useQuery({
        queryKey: ["admin", "leagues", "settings"],
        queryFn: () => apiClient.get<AdminLeagueSettings>("/admin/leagues/settings"),
    });
}

export function useUpdateLeagueSettings() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: AdminLeagueSettings) =>
            apiClient.put<AdminLeagueSettings>("/admin/leagues/settings", body),
        onSuccess: (data) => {
            clientLogger.info("League settings updated", { ...data });
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to update league settings", { error: (error as Error).message });
        },
    });
}

export function useCloseCurrentLeagueWeek() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => apiClient.post<void>("/admin/leagues/close-current", {}),
        onSuccess: () => {
            clientLogger.warn("League week manually closed");
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to close league week", { error: (error as Error).message });
        },
    });
}

export function useResyncLeague() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (leagueId: string) =>
            apiClient.post<AdminLeagueDetail>(`/admin/leagues/${leagueId}/resync`, {}),
        onSuccess: (data) => {
            clientLogger.info("League XP resynced", { leagueId: data.id });
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error, leagueId) => {
            clientLogger.error("Failed to resync league", { leagueId, error: (error as Error).message });
        },
    });
}

export function useMoveMembershipTier() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ membershipId, tier }: { membershipId: string; tier: string }) =>
            apiClient.put<AdminLeagueDetail>(`/admin/leagues/memberships/${membershipId}/tier`, { tier }),
        onSuccess: (data, variables) => {
            clientLogger.info("League membership tier changed", {
                membershipId: variables.membershipId,
                newTier: variables.tier,
                leagueId: data.id,
            });
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to move membership tier", {
                membershipId: variables.membershipId,
                tier: variables.tier,
                error: (error as Error).message,
            });
        },
    });
}

export function useAdjustMembershipXp() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ membershipId, delta }: { membershipId: string; delta: number }) =>
            apiClient.put<AdminLeagueDetail>(`/admin/leagues/memberships/${membershipId}/xp`, { delta }),
        onSuccess: (data, variables) => {
            clientLogger.info("League membership XP adjusted", {
                membershipId: variables.membershipId,
                delta: variables.delta,
                leagueId: data.id,
            });
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to adjust membership XP", {
                membershipId: variables.membershipId,
                delta: variables.delta,
                error: (error as Error).message,
            });
        },
    });
}

export function useRemoveLeagueMembership() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (membershipId: string) =>
            apiClient.delete<void>(`/admin/leagues/memberships/${membershipId}`),
        onSuccess: (_, membershipId) => {
            clientLogger.warn("League membership removed", { membershipId });
            queryClient.invalidateQueries({ queryKey: ["admin", "leagues"] });
        },
        onError: (error, membershipId) => {
            clientLogger.error("Failed to remove league membership", { membershipId, error: (error as Error).message });
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
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ exerciseType, systemPrompt }: { exerciseType: string; systemPrompt: string }) =>
            apiClient.put<ExerciseTypePrompt>(`/admin/exercise-type-prompts/${exerciseType}`, { systemPrompt }),
        onSuccess: (data) => {
            clientLogger.info("Exercise type prompt updated", { exerciseType: data.exerciseType });
            queryClient.invalidateQueries({ queryKey: ["admin", "exercise-type-prompts"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to update exercise type prompt", { exerciseType: variables.exerciseType, error: (error as Error).message });
        },
    });
}
