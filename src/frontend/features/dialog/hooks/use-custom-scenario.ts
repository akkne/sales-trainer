import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";

/** Mirrors ScenarioLimits on the backend — the same bounds are enforced there. */
export const SCENARIO_MIN_LENGTH = 20;
export const SCENARIO_MAX_LENGTH = 1500;

export interface DialogModeIdentifier {
    bundleId: string;
    modeId: string;
}

export interface ScenarioValidationResponse {
    isValid: boolean;
    rejectionReason: string | null;
}

/**
 * Ids of the hidden custom-scenario bundle/mode. Hidden bundles never appear in
 * GET /dialog/bundles, so they have to be resolved through this endpoint.
 */
export function useCustomScenarioMode() {
    return useQuery({
        queryKey: ["dialog", "custom-scenario-mode"],
        queryFn: () => apiClient.get<DialogModeIdentifier>("/dialog/custom-scenario-mode"),
        // The ids never change once seeded, so a hit is cached for the session. A miss is
        // retried, though: without it one hiccup leaves the practice page with no way in
        // until a full reload.
        staleTime: Infinity,
        retry: 1,
    });
}

/**
 * Pre-flight relevance check, so the compose dialog can say "не про продажи" without dropping the
 * user into a conversation first. Advisory: POST /dialog/sessions re-checks server-side, which is
 * what actually enforces the rule.
 */
export async function validateScenario(scenario: string): Promise<ScenarioValidationResponse> {
    return apiClient.post<ScenarioValidationResponse>("/dialog/scenario/validate", { scenario });
}
