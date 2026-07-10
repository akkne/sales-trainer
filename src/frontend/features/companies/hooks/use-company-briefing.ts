import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export interface CompanyBriefing {
    content: string | null;
    generatedAt: string | null;
}

const briefingKey = (companyId: string) => ["companies", companyId, "briefing"] as const;

export function useCompanyBriefing(companyId: string | null) {
    return useQuery({
        queryKey: briefingKey(companyId ?? ""),
        queryFn: async () => {
            // GET returns 204 (no body) when a briefing has never been generated; the api client
            // resolves a 204 to `undefined`, but React Query query functions may not return
            // `undefined` — normalize to an explicit empty briefing instead.
            const result = await apiClient.get<CompanyBriefing | undefined>(`/companies/${companyId}/briefing`);
            return result ?? { content: null, generatedAt: null };
        },
        enabled: !!companyId,
    });
}

export function useGenerateCompanyBriefing(companyId: string | null) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => apiClient.post<CompanyBriefing>(`/companies/${companyId}/briefing`, {}),
        onSuccess: (data) => {
            if (!companyId) return;
            queryClient.setQueryData(briefingKey(companyId), data);
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сгенерировать шпаргалку: ${error.message}`);
        },
    });
}
