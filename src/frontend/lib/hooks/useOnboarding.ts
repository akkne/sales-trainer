import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/apiClient";
import { useAuthStore } from "@/lib/store/authStore";

interface OnboardingPayload {
    salesType: string;
    experienceLevel: string;
    selectedSkillSlugs: string[];
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
