import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface AdminDialogBundle {
    id: string;
    skillId: string;
    skillSlug: string;
    skillTitle: string;
    title: string;
    description: string;
    iconEmoji: string;
    sortOrder: number;
    isActive: boolean;
}

export interface AdminDialogMode {
    id: string;
    bundleId: string;
    key: string;
    title: string;
    description: string;
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
    sortOrder: number;
    isActive: boolean;
}

export interface CreateBundleRequest {
    skillId: string;
    title: string;
    description: string;
    iconEmoji: string;
    sortOrder: number;
    isActive: boolean;
}

export interface UpdateBundleRequest {
    skillId?: string;
    title?: string;
    description?: string;
    iconEmoji?: string;
    sortOrder?: number;
    isActive?: boolean;
}

export interface CreateModeRequest {
    key: string;
    title: string;
    description: string;
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
    sortOrder: number;
    isActive: boolean;
}

export interface UpdateModeRequest {
    key?: string;
    title?: string;
    description?: string;
    chatSystemPrompt?: string;
    feedbackSystemPrompt?: string;
    sortOrder?: number;
    isActive?: boolean;
}

export interface AdminSkill {
    id: string;
    slug: string;
    title: string;
}

export function useAdminDialogBundles() {
    return useQuery({
        queryKey: ["admin", "dialog", "bundles"],
        queryFn: () => apiClient.get<AdminDialogBundle[]>("/admin/dialog/bundles"),
    });
}

export function useAdminDialogModes(bundleId: string) {
    return useQuery({
        queryKey: ["admin", "dialog", "bundles", bundleId, "modes"],
        queryFn: () => apiClient.get<AdminDialogMode[]>(`/admin/dialog/bundles/${bundleId}/modes`),
        enabled: !!bundleId,
    });
}

export function useAdminSkills() {
    return useQuery({
        queryKey: ["admin", "skills"],
        queryFn: () => apiClient.get<AdminSkill[]>("/admin/skills"),
    });
}

export function useCreateBundle() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (request: CreateBundleRequest) =>
            apiClient.post<AdminDialogBundle>("/admin/dialog/bundles", request),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles"] });
        },
    });
}

export function useUpdateBundle() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ bundleId, request }: { bundleId: string; request: UpdateBundleRequest }) =>
            apiClient.put<AdminDialogBundle>(`/admin/dialog/bundles/${bundleId}`, request),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles"] });
        },
    });
}

export function useDeleteBundle() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (bundleId: string) =>
            apiClient.delete(`/admin/dialog/bundles/${bundleId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles"] });
        },
    });
}

export function useCreateMode() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ bundleId, request }: { bundleId: string; request: CreateModeRequest }) =>
            apiClient.post<AdminDialogMode>(`/admin/dialog/bundles/${bundleId}/modes`, request),
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles", variables.bundleId, "modes"] });
        },
    });
}

export function useUpdateMode() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ modeId, request }: { modeId: string; request: UpdateModeRequest }) =>
            apiClient.put<AdminDialogMode>(`/admin/dialog/modes/${modeId}`, request),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog"] });
        },
    });
}

export function useDeleteMode() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (modeId: string) =>
            apiClient.delete(`/admin/dialog/modes/${modeId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog"] });
        },
    });
}
