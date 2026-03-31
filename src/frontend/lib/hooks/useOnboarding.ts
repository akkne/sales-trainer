import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { apiClient } from "@/lib/api/apiClient";
import { useAuthStore } from "@/lib/store/authStore";

interface OnboardingPayload {
    salesType: string;
    experienceLevel: string;
    goal: string;
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
