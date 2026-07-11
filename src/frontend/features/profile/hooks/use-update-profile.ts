import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

export interface UpdateProfilePayload {
    displayName: string;
    /** One of the persona slugs, or null to leave the current persona unchanged. */
    persona: string | null;
}

/**
 * Mutation: update the current user's display name (and optionally persona).
 * Refetches ["profile"] so the identity row reflects the new values.
 */
export function useUpdateProfile() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (payload: UpdateProfilePayload) =>
            apiClient.put<void>("/profile", payload),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["profile"] });
        },
    });
}
