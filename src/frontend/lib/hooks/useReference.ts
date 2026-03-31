import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface ReferenceMaterial {
    materialId: string;
    title: string;
    markdownContent: string;
    sortOrder: number;
}

export function useReferenceMaterials(skillSlug: string) {
    return useQuery({
        queryKey: ["reference", skillSlug],
        queryFn: () =>
            apiClient.get<ReferenceMaterial[]>(`/skills/${skillSlug}/reference`),
    });
}
