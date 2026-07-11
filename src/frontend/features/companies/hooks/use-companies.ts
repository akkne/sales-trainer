import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";
import type { CompanyStatus } from "@/features/companies/lib/company-status";

export interface CompanySummary {
    id: string;
    name: string;
    descriptionExcerpt: string;
    status: CompanyStatus;
    callLogCount: number;
    practiceCallCount: number;
    contactCount: number;
    nextActionAt: string | null;
    createdAt: string;
    updatedAt: string;
}

export interface CompanyDetail {
    id: string;
    name: string;
    description: string;
    status: CompanyStatus;
    callLogCount: number;
    practiceCallCount: number;
    contactCount: number;
    nextActionAt: string | null;
    nextActionNote: string | null;
    followUpNotifiedAt: string | null;
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

export function useUpdateCompanyStatus() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, status }: { id: string; status: CompanyStatus }) =>
            apiClient.put<CompanyDetail>(`/companies/${id}/status`, { status }),
        // Optimistically flip the status in both caches so the badge/menu reflects
        // the change instantly instead of waiting for the round-trip. Rolled back
        // in onError; onSuccess re-syncs with the server response.
        onMutate: async ({ id, status }: { id: string; status: CompanyStatus }) => {
            await queryClient.cancelQueries({ queryKey: companiesKey });
            await queryClient.cancelQueries({ queryKey: companyKey(id) });

            const previousList = queryClient.getQueryData<CompanySummary[]>(companiesKey);
            const previousDetail = queryClient.getQueryData<CompanyDetail>(companyKey(id));

            queryClient.setQueryData<CompanySummary[]>(companiesKey, (current) =>
                current?.map((company) => (company.id === id ? { ...company, status } : company))
            );
            queryClient.setQueryData<CompanyDetail>(companyKey(id), (current) =>
                current ? { ...current, status } : current
            );

            return { previousList, previousDetail, id };
        },
        onSuccess: (_data, { id }) => {
            queryClient.invalidateQueries({ queryKey: companiesKey });
            queryClient.invalidateQueries({ queryKey: companyKey(id) });
        },
        onError: (error: Error, _variables, context) => {
            if (context?.previousList) {
                queryClient.setQueryData(companiesKey, context.previousList);
            }
            if (context) {
                queryClient.setQueryData(companyKey(context.id), context.previousDetail);
            }
            toast.error(`Не удалось изменить статус: ${error.message}`);
        },
    });
}

export function useUpdateCompanyFollowUp() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ id, nextActionAt, nextActionNote }: { id: string; nextActionAt: string | null; nextActionNote: string | null }) =>
            apiClient.put<CompanyDetail>(`/companies/${id}/follow-up`, { nextActionAt, nextActionNote }),
        onSuccess: (_data, { id }) => {
            queryClient.invalidateQueries({ queryKey: companiesKey });
            queryClient.invalidateQueries({ queryKey: companyKey(id) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить напоминание: ${error.message}`);
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
