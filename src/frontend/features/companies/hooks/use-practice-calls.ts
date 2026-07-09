import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface PracticeCall {
    id: string;
    companyId: string;
    dialogSessionId: string;
    goal: string;
    createdAt: string;
}

const practiceCallsKey = (companyId: string) => ["companies", companyId, "practice-calls"] as const;
const recentGoalsKey = (companyId: string) => ["companies", companyId, "recent-goals"] as const;

export function useCompanyPracticeCalls(companyId: string | null) {
    return useQuery({
        queryKey: practiceCallsKey(companyId ?? ""),
        queryFn: () => apiClient.get<PracticeCall[]>(`/companies/${companyId}/practice-calls`),
        enabled: !!companyId,
    });
}

export function useRecentGoals(companyId: string | null) {
    return useQuery({
        queryKey: recentGoalsKey(companyId ?? ""),
        queryFn: () => apiClient.get<string[]>(`/companies/${companyId}/recent-goals`),
        enabled: !!companyId,
    });
}

export function useCreatePracticeCall(companyId: string | null) {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (body: { dialogSessionId: string; goal: string }) =>
            apiClient.post<PracticeCall>(`/companies/${companyId}/practice-calls`, body),
        onSuccess: () => {
            if (!companyId) return;
            queryClient.invalidateQueries({ queryKey: practiceCallsKey(companyId) });
            queryClient.invalidateQueries({ queryKey: recentGoalsKey(companyId) });
        },
    });
}
