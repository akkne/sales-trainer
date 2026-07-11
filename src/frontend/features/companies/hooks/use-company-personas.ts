import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export type PersonaDifficulty = "Easy" | "Medium" | "Hard";

export interface CompanyPersona {
    id: string;
    companyId: string;
    name: string;
    position: string;
    personality: string;
    difficulty: PersonaDifficulty;
    createdAt: string;
}

export interface CompanyPersonaPayload {
    name: string;
    position: string;
    personality: string;
    difficulty: PersonaDifficulty;
}

export interface GeneratePersonaPayload {
    contactName?: string;
    contactPosition?: string;
    difficulty: PersonaDifficulty;
}

export interface GeneratedPersona {
    name: string;
    position: string;
    personality: string;
}

const personasKey = (companyId: string) => ["companies", companyId, "personas"] as const;

export function useCompanyPersonas(companyId: string | null) {
    return useQuery({
        queryKey: personasKey(companyId ?? ""),
        queryFn: () => apiClient.get<CompanyPersona[]>(`/companies/${companyId}/personas`),
        enabled: !!companyId,
    });
}

export function useAddCompanyPersona(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: CompanyPersonaPayload) =>
            apiClient.post<CompanyPersona>(`/companies/${companyId}/personas`, body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: personasKey(companyId) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить собеседника: ${error.message}`);
        },
    });
}

export function useDeleteCompanyPersona(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (personaId: string) => apiClient.delete<void>(`/companies/${companyId}/personas/${personaId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: personasKey(companyId) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось удалить собеседника: ${error.message}`);
        },
    });
}

export function useGenerateCompanyPersona(companyId: string | null) {
    return useMutation({
        mutationFn: (body: GeneratePersonaPayload) =>
            apiClient.post<GeneratedPersona>(`/companies/${companyId}/personas/generate`, body),
        onError: (error: Error) => {
            toast.error(`Не удалось сгенерировать собеседника: ${error.message}`);
        },
    });
}
