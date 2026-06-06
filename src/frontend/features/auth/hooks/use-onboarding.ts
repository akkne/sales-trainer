import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { apiClient } from "@/shared/api/api-client";
import { useAuthStore } from "@/shared/stores/auth-store";

interface OnboardingPayload {
    salesType: string;
    experienceLevel: string;
    selectedSkillSlugs: string[];
    persona?: string;
}

export function useCompleteOnboarding() {
    const router = useRouter();
    const { authenticatedUser, setAuthenticatedUser } = useAuthStore();

    return useMutation({
        mutationFn: (payload: OnboardingPayload) =>
            apiClient.post<void>("/onboarding", payload),
        onSuccess: () => {
            if (authenticatedUser) {
                setAuthenticatedUser({
                    ...authenticatedUser,
                    isOnboardingCompleted: true,
                });
            }
            router.push("/tree");
        },
    });
}

/** Returns all skills from the backend (used during onboarding to show selection). */
export function useSkillsForOnboarding() {
    return useQuery({
        queryKey: ["skills-onboarding"],
        queryFn: () =>
            apiClient.get<{ skillId: string; slug: string; title: string; iconName: string }[]>(
                "/skills"
            ),
    });
}
