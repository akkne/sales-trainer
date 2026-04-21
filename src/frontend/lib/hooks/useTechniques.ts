import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/apiClient";

export interface TechniqueCard {
    id: string;
    slug: string;
    name: string;
    summary: string;
    tags: string[];
    primarySkillIconicName: string | null;
    primarySkillTitle: string | null;
    difficulty: number;
    difficultyName: string;
    sortOrder: number;
    masteryLevel: number;
    masteryPercent: number;
    hasDialog: boolean;
    hasCase: boolean;
    hasCoach: boolean;
    isNew: boolean;
}

export interface TechniqueDialogAnnotation {
    label: string;
    tone: string | null;
}

export interface TechniqueDialogTurn {
    orderIndex: number;
    side: "me" | "them" | string;
    text: string;
    annotations: TechniqueDialogAnnotation[];
}

export interface TechniqueCase {
    title: string;
    body: string;
    metrics: Record<string, unknown> | null;
}

export interface TechniqueCoachChallenge {
    label: string;
    kind: string | null;
    targetSlug: string | null;
}

export interface TechniqueCoach {
    avatarSeed: string;
    name: string;
    role: string;
    quote: string;
    challenges: TechniqueCoachChallenge[];
}

export interface TechniqueDetail {
    card: TechniqueCard;
    body: string;
    skillIconicNames: string[];
    dialogTurns: TechniqueDialogTurn[];
    case: TechniqueCase | null;
    coach: TechniqueCoach | null;
}

export interface TechniqueSkillFacet {
    iconicName: string;
    title: string;
    techniqueCount: number;
}

export interface TechniqueMeta {
    skills: TechniqueSkillFacet[];
    totalCount: number;
    userCounts: {
        mastered: number;
        master: number;
        unseen: number;
    };
}

export function useTechniques(params: {
    skill?: string;
    search?: string;
    tag?: string;
}) {
    const { skill, search, tag } = params;
    return useQuery({
        queryKey: ["techniques", skill ?? "", search ?? "", tag ?? ""],
        queryFn: () => {
            const query = new URLSearchParams();
            if (skill) query.set("skill", skill);
            if (search) query.set("search", search);
            if (tag) query.set("tag", tag);
            const tail = query.toString();
            return apiClient.get<TechniqueCard[]>(
                `/techniques${tail ? `?${tail}` : ""}`
            );
        },
    });
}

export function useTechniquesMeta() {
    return useQuery({
        queryKey: ["techniques-meta"],
        queryFn: () => apiClient.get<TechniqueMeta>("/techniques/meta"),
    });
}

export function useTechnique(slug: string | null) {
    return useQuery({
        queryKey: ["technique", slug],
        enabled: !!slug,
        queryFn: () =>
            apiClient.get<TechniqueDetail>(`/techniques/${slug!}`),
    });
}

export function useMarkTechniqueSeen() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (slug: string) =>
            apiClient.post<void>(`/techniques/${slug}/seen`, {}),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["techniques"] });
            queryClient.invalidateQueries({ queryKey: ["techniques-meta"] });
        },
    });
}
