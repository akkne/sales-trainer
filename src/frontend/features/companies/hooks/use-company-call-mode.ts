import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface CompanyCallMode {
    bundleId: string;
    modeId: string;
}

export function useCompanyCallMode() {
    return useQuery({
        queryKey: ["dialog", "company-call-mode"],
        queryFn: () => apiClient.get<CompanyCallMode>("/dialog/company-call-mode"),
        retry: false,
    });
}
