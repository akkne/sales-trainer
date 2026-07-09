import { useQuery } from "@tanstack/react-query";
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

/** Practice-call history for a company (§3.4a of the design spec). */
export function useCompanyPracticeCalls(companyId: string | null) {
    return useQuery({
        queryKey: practiceCallsKey(companyId ?? ""),
        queryFn: () => apiClient.get<PracticeCall[]>(`/companies/${companyId}/practice-calls`),
        enabled: !!companyId,
    });
}

/** Up to 5 recent goals used for this company's practice calls (§4.1 of the design spec). */
export function useRecentGoals(companyId: string | null) {
    return useQuery({
        queryKey: recentGoalsKey(companyId ?? ""),
        queryFn: () => apiClient.get<string[]>(`/companies/${companyId}/recent-goals`),
        enabled: !!companyId,
    });
}
