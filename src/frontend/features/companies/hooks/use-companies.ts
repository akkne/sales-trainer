import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export interface CompanySummary {
    id: string;
    name: string;
    descriptionExcerpt: string;
    callLogCount: number;
    practiceCallCount: number;
    contactCount: number;
    createdAt: string;
    updatedAt: string;
}

export interface CompanyDetail {
    id: string;
    name: string;
    description: string;
    callLogCount: number;
    practiceCallCount: number;
    contactCount: number;
    createdAt: string;
    updatedAt: string;
}

const companiesKey = ["companies"] as const;
const companyKey = (id: string) => ["companies", id] as const;

export function useCompanies(search?: string) {
    const trimmed = search?.trim().toLowerCase() ?? "";
    return useQuery({
        queryKey: companiesKey,
        queryFn: () => apiClient.get<CompanySummary[]>("/companies"),
        select: (companies) =>
            trimmed
                ? companies.filter((company) => company.name.toLowerCase().includes(trimmed))
                : companies,
    });
}

export function useCompany(id: string | null) {
    return useQuery({
        queryKey: companyKey(id ?? ""),
        queryFn: () => apiClient.get<CompanyDetail>(`/companies/${id}`),
        enabled: !!id,
    });
}

export function useCreateCompany() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: { name: string; description?: string }) =>
            apiClient.post<CompanyDetail>("/companies", body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: companiesKey });
        },
    });
}

export function useUpdateCompany() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, ...body }: { id: string; name: string; description: string }) =>
            apiClient.put<CompanyDetail>(`/companies/${id}`, body),
        onSuccess: (_data, { id }) => {
            queryClient.invalidateQueries({ queryKey: companiesKey });
            queryClient.invalidateQueries({ queryKey: companyKey(id) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить компанию: ${error.message}`);
        },
    });
}

export function useDeleteCompany() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => apiClient.delete<void>(`/companies/${id}`),
        onSuccess: (_data, id) => {
            queryClient.invalidateQueries({ queryKey: companiesKey });
            queryClient.removeQueries({ queryKey: companyKey(id) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось удалить компанию: ${error.message}`);
        },
    });
}
