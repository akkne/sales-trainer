import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface ReferenceMaterial {
    materialId: string;
    title: string;
    markdownContent: string;
    sortOrder: number;
    category: string | null;
    tags: string[];
    skillSlug: string;
}

export function useReferenceMaterials(skillSlug: string) {
    return useQuery({
        queryKey: ["reference", skillSlug],
        queryFn: () =>
            apiClient.get<ReferenceMaterial[]>(`/skills/${skillSlug}/reference`),
    });
}

export function useHandbook(category?: string, search?: string) {
    return useQuery({
        queryKey: ["handbook", category ?? "", search ?? ""],
        queryFn: () => {
            const params = new URLSearchParams();
            if (category) params.set("category", category);
            if (search) params.set("search", search);
            const qs = params.toString();
            return apiClient.get<ReferenceMaterial[]>(`/reference${qs ? `?${qs}` : ""}`);
        },
    });
}

export function useHandbookCategories() {
    return useQuery({
        queryKey: ["handbook-categories"],
        queryFn: () => apiClient.get<string[]>("/reference/categories"),
    });
}
