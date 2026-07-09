import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export interface CallLogEntry {
    id: string;
    companyId: string;
    contactName: string;
    subject: string;
    outcome: string;
    occurredAt: string;
    createdAt: string;
    updatedAt: string;
}

export interface CallLogPayload {
    contactName: string;
    subject: string;
    outcome: string;
    occurredAt: string;
}

const logsKey = (companyId: string) => ["companies", companyId, "logs"] as const;
const companyKey = (id: string) => ["companies", id] as const;
const companiesKey = ["companies"] as const;

export function useCompanyLogs(companyId: string | null) {
    return useQuery({
        queryKey: logsKey(companyId ?? ""),
        queryFn: () => apiClient.get<CallLogEntry[]>(`/companies/${companyId}/logs`),
        enabled: !!companyId,
    });
}

export function useAddCallLog(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: CallLogPayload) =>
            apiClient.post<CallLogEntry>(`/companies/${companyId}/logs`, body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: logsKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companyKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companiesKey });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить запись: ${error.message}`);
        },
    });
}

export function useUpdateCallLog(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ logId, ...body }: CallLogPayload & { logId: string }) =>
            apiClient.put<CallLogEntry>(`/companies/${companyId}/logs/${logId}`, body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: logsKey(companyId) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить запись: ${error.message}`);
        },
    });
}

export function useDeleteCallLog(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (logId: string) => apiClient.delete<void>(`/companies/${companyId}/logs/${logId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: logsKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companyKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companiesKey });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось удалить запись: ${error.message}`);
        },
    });
}
