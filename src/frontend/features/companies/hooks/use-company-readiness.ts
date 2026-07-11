import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface CompanyReadiness {
    score: number | null;
    strengths: string[] | null;
    gaps: string[] | null;
    recommendation: string | null;
    generatedAt: string | null;
}

const readinessKey = (companyId: string) => ["companies", companyId, "readiness"] as const;

const EMPTY_READINESS: CompanyReadiness = {
    score: null,
    strengths: null,
    gaps: null,
    recommendation: null,
    generatedAt: null,
};

export function useCompanyReadiness(companyId: string | null) {
    return useQuery({
        queryKey: readinessKey(companyId ?? ""),
        queryFn: async () => {
            // GET self-generates and caches on the backend; it returns 204 (no body) when the
            // company has no practice sessions yet (or ai-service found no usable feedback). The
            // api client resolves a 204 to `undefined` — normalize to an explicit empty readiness.
            const result = await apiClient.get<CompanyReadiness | undefined>(`/companies/${companyId}/readiness`);
            return result ?? EMPTY_READINESS;
        },
        enabled: !!companyId,
    });
}
