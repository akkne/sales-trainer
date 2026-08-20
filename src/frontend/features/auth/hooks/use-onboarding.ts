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
        mutationFn: async (payload: OnboardingPayload) => {
            await apiClient.post<void>("/onboarding", payload);
            // Persist the chosen skills as the user's enrolled set (core skill is
            // always kept by the backend). Let a failure here fail the whole mutation
            // instead of swallowing it — onSuccess marks onboarding complete and routes
            // to /tree, so a silent catch here meant the user's skill choice could vanish
            // with no error and no chance to retry (docs/AUDIT_SILENT_WRITES.md W-13).
            await apiClient.put<void>("/skills/enrolled", {
                skillSlugs: payload.selectedSkillSlugs,
            });
        },
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
