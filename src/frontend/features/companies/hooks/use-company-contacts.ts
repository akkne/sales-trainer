import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { toast } from "@/features/notifications/store/toast-store";

export interface CompanyContact {
    id: string;
    companyId: string;
    name: string;
    position: string;
    notes: string;
    createdAt: string;
    updatedAt: string;
}

export interface CompanyContactPayload {
    name: string;
    position: string;
    notes: string;
}

const contactsKey = (companyId: string) => ["companies", companyId, "contacts"] as const;
const companyKey = (id: string) => ["companies", id] as const;
const companiesKey = ["companies"] as const;

export function useCompanyContacts(companyId: string | null) {
    return useQuery({
        queryKey: contactsKey(companyId ?? ""),
        queryFn: () => apiClient.get<CompanyContact[]>(`/companies/${companyId}/contacts`),
        enabled: !!companyId,
    });
}

export function useAddCompanyContact(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: CompanyContactPayload) =>
            apiClient.post<CompanyContact>(`/companies/${companyId}/contacts`, body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: contactsKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companyKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companiesKey });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить контакт: ${error.message}`);
        },
    });
}

export function useUpdateCompanyContact(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ contactId, ...body }: CompanyContactPayload & { contactId: string }) =>
            apiClient.put<CompanyContact>(`/companies/${companyId}/contacts/${contactId}`, body),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: contactsKey(companyId) });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось сохранить контакт: ${error.message}`);
        },
    });
}

export function useDeleteCompanyContact(companyId: string) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (contactId: string) => apiClient.delete<void>(`/companies/${companyId}/contacts/${contactId}`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: contactsKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companyKey(companyId) });
            queryClient.invalidateQueries({ queryKey: companiesKey });
        },
        onError: (error: Error) => {
            toast.error(`Не удалось удалить контакт: ${error.message}`);
        },
    });
}
