import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { apiClient } from "@/shared/api/api-client";
import { useAuthStore } from "@/shared/stores/auth-store";
import { useOnboardingSkillSelectionStore } from "@/shared/stores/onboarding-skill-selection-store";
import { clientLogger } from "@/shared/utils/client-logger";

interface OnboardingPayload {
    salesType: string;
    experienceLevel: string;
    selectedSkillSlugs: string[];
    persona?: string;
}

/**
 * Onboarding's two writes, in order, with only the first of them able to fail the whole thing.
 *
 * `POST /onboarding` is the write that decides whether onboarding happened at all, so it is allowed
 * to throw and keep the user on the screen. `PUT /skills/enrolled` is best-effort: a failure there
 * must not trap a new user behind a retry they cannot influence, so it is caught — but it is *not*
 * swallowed, which is what W-13 was actually about (docs/AUDIT_SILENT_WRITES.md). The
 * `didPersistSkillSelection: false` it returns is what puts the honest line on `/tree`
 * (Q-6, docs/NIGHT_AUDIT_QUESTIONS.md — the owner picked "tell them on /tree" over both
 * "fail the whole onboarding" and "stay silent").
 *
 * Split out of the mutation so this ordering is testable without a React tree, the same way
 * `completeDialogSession` is.
 */
export async function submitOnboarding(
    payload: OnboardingPayload
): Promise<{ didPersistSkillSelection: boolean }> {
    await apiClient.post<void>("/onboarding", payload);

    try {
        await apiClient.put<void>("/skills/enrolled", {
            skillSlugs: payload.selectedSkillSlugs,
        });
        return { didPersistSkillSelection: true };
    } catch (error) {
        clientLogger.error("Onboarding could not persist the selected skills", {
            error: (error as Error).message,
        });
        return { didPersistSkillSelection: false };
    }
}

export function useCompleteOnboarding() {
    const router = useRouter();
    const { authenticatedUser, setAuthenticatedUser } = useAuthStore();
    const { markSkillSelectionUnsaved, clearSkillSelectionUnsaved } =
        useOnboardingSkillSelectionStore();

    return useMutation({
        mutationFn: submitOnboarding,
        onSuccess: ({ didPersistSkillSelection }) => {
            if (didPersistSkillSelection) {
                clearSkillSelectionUnsaved();
            } else {
                markSkillSelectionUnsaved();
            }

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
