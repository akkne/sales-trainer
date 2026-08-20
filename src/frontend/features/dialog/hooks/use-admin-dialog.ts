import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { toast } from "@/features/notifications/store/toast-store";

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
    isHidden: boolean;
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
    voiceEnabled: boolean;
    voiceId: string | null;
}

export interface CreateBundleRequest {
    skillId: string;
    title: string;
    description: string;
    iconEmoji: string;
    sortOrder: number;
    isActive: boolean;
    isHidden?: boolean;
}

export interface UpdateBundleRequest {
    skillId?: string;
    title?: string;
    description?: string;
    iconEmoji?: string;
    sortOrder?: number;
    isActive?: boolean;
    isHidden?: boolean;
}

export interface CreateModeRequest {
    key: string;
    title: string;
    description: string;
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
    sortOrder: number;
    isActive: boolean;
    voiceEnabled?: boolean;
    voiceId?: string | null;
}

export interface UpdateModeRequest {
    key?: string;
    title?: string;
    description?: string;
    chatSystemPrompt?: string;
    feedbackSystemPrompt?: string;
    sortOrder?: number;
    isActive?: boolean;
    voiceEnabled?: boolean;
    voiceId?: string | null;
}

export interface AdminSkill {
    id: string;
    iconicName: string;
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

export interface DialogImportResult {
    bundlesCreated: number;
    bundlesUpdated: number;
    modesCreated: number;
    modesUpdated: number;
    errors: string[];
}

/** Import dialog bundles (with nested modes) from one JSON file. */
export function useImportDialog() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (file: File) => {
            const formData = new FormData();
            formData.append("file", file);
            return apiClient.postFile<DialogImportResult>("/admin/dialog/import", formData);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles"] });
        },
        onError: (error) => {
            clientLogger.error("Failed to import dialog bundles", { error: (error as Error).message });
            toast.error(`Failed to import dialog bundles: ${(error as Error).message}`);
        },
    });
}

export interface DialogExportMode {
    key: string;
    title: string;
    description: string;
    chatSystemPrompt: string;
    feedbackSystemPrompt: string;
    sortOrder: number;
    isActive: boolean;
    voiceEnabled: boolean;
    voiceId: string | null;
}

export interface DialogExportBundle {
    skillId: string;
    title: string;
    description: string;
    iconEmoji: string;
    sortOrder: number;
    isActive: boolean;
    modes: DialogExportMode[];
}

export interface DialogExport {
    bundles: DialogExportBundle[];
}

/** Fetch all dialog bundles (with nested modes), shaped to re-import verbatim. */
export function fetchDialogExport() {
    return apiClient.get<DialogExport>("/admin/dialog/export");
}

export function useCreateBundle() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (request: CreateBundleRequest) =>
            apiClient.post<AdminDialogBundle>("/admin/dialog/bundles", request),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["admin", "dialog", "bundles"] });
        },
        onError: (error, variables) => {
            clientLogger.error("Failed to create dialog bundle", { title: variables.title, error: (error as Error).message });
            toast.error(`Failed to create dialog bundle: ${(error as Error).message}`);
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
        onError: (error, variables) => {
            clientLogger.error("Failed to update dialog bundle", { bundleId: variables.bundleId, error: (error as Error).message });
            toast.error(`Failed to update dialog bundle: ${(error as Error).message}`);
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
        onError: (error, bundleId) => {
            clientLogger.error("Failed to delete dialog bundle", { bundleId, error: (error as Error).message });
            toast.error(`Failed to delete dialog bundle: ${(error as Error).message}`);
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
        onError: (error, variables) => {
            clientLogger.error("Failed to create dialog mode", { bundleId: variables.bundleId, error: (error as Error).message });
            toast.error(`Failed to create dialog mode: ${(error as Error).message}`);
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
        onError: (error, variables) => {
            clientLogger.error("Failed to update dialog mode", { modeId: variables.modeId, error: (error as Error).message });
            toast.error(`Failed to update dialog mode: ${(error as Error).message}`);
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
        onError: (error, modeId) => {
            clientLogger.error("Failed to delete dialog mode", { modeId, error: (error as Error).message });
            toast.error(`Failed to delete dialog mode: ${(error as Error).message}`);
        },
    });
}
