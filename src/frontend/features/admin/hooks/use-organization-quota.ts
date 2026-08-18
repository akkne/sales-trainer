"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { TimingConstants } from "@/shared/constants/timing-constants";

/**
 * The platform panel's quota screen (docs/TENANCY/ADMIN_UI_DESIGN.md §3.2), against
 * `GET`/`PUT /admin/ai-quota` and the read-only `GET /admin/ai-usage` beside them.
 *
 * Neither quota route carries an organization anywhere a caller can set: ai-service resolves the
 * tenant from `X-Organization-Id`, which the gateway writes from the validated token and from
 * nowhere else (`IdentityForwarding.Apply`). The `[organizationId]` in this screen's URL therefore
 * names *which organization the operator believes they are looking at*, and the screen has to check
 * that belief against the session rather than send it — see `resolveQuotaEditability`.
 */

/** `AiQuotaSettingsDto`. A null field means "no organization-specific value, the platform default applies". */
export interface OrganizationQuotaSettings {
    voiceDailyLimitMinutes: number | null;
    voiceMonthlyLimitMinutes: number | null;
    llmMonthlyTokenLimit: number | null;
    batchReservePercent: number | null;
    note: string | null;
    /** False when no `OrganizationQuotas` row exists and every effective number below is a platform default. */
    isOrganizationSpecific: boolean;
    effectiveVoiceDailyLimitMinutes: number;
    effectiveVoiceMonthlyLimitMinutes: number;
    effectiveLlmMonthlyTokenLimit: number;
    effectiveBatchReservePercent: number;
    updatedAt: string | null;
}

/** `AiQuotaWriteModel`. An omitted (null) field clears the organization's value back to the platform default. */
export interface OrganizationQuotaWriteModel {
    voiceDailyLimitMinutes: number | null;
    voiceMonthlyLimitMinutes: number | null;
    llmMonthlyTokenLimit: number | null;
    batchReservePercent: number | null;
    note: string | null;
}

/** `AiSpendReportDto.QuotaState` — mirrors `Sellevate.Ai.Features.Quotas.Constants.AiQuotaStates`. */
export type OrganizationQuotaState = "ok" | "warning" | "batch_paused" | "exhausted";

/** `AiSpendModelLineDto`. */
export interface OrganizationSpendModelLine {
    model: string;
    kind: string;
    promptTokens: number;
    completionTokens: number;
    callCount: number;
    speechCharacters: number;
    /** Null when this model has no entry in the price table — never a stand-in `0`. */
    estimatedCost: number | null;
}

/** `AiSpendReportDto`. Always the current UTC calendar month; the endpoint takes no parameters at all. */
export interface OrganizationSpendReport {
    periodKey: string;
    currency: string;
    quotaState: OrganizationQuotaState;
    llmPromptTokens: number;
    llmCompletionTokens: number;
    llmTotalTokens: number;
    llmMonthlyTokenLimit: number;
    llmCallCount: number;
    llmEstimatedCallCount: number;
    speechCharacters: number;
    voiceUsedMinutesToday: number;
    voiceDailyLimitMinutes: number;
    voiceUsedMinutesThisMonth: number;
    voiceMonthlyLimitMinutes: number;
    /** Null as soon as any model used this month is unpriced — a partial total is worse than none. */
    estimatedCost: number | null;
    hasUnpricedModels: boolean;
    models: OrganizationSpendModelLine[];
}

/** `OrganizationDetailDto` from organization-service's tenant registry. */
export interface PlatformOrganizationDetail {
    id: string;
    name: string;
    slug: string;
    status: string;
    createdAt: string;
    updatedAt: string;
}

const organizationQuotaQueryKey = ["admin", "ai-quota"];
const organizationSpendQueryKey = ["admin", "ai-usage"];

/**
 * `GET /admin/ai-quota` — `RequirePlatformAdministrator`. Answers for the session's own
 * organization, and for a session carrying none it answers the platform defaults with
 * `isOrganizationSpecific: false` rather than 404.
 */
export function useOrganizationQuotaSettings() {
    return useQuery({
        queryKey: organizationQuotaQueryKey,
        queryFn: () => apiClient.get<OrganizationQuotaSettings>("/admin/ai-quota"),
    });
}

/**
 * `GET /admin/ai-usage` — the spend the limit above is measured against. Read-only here: the
 * money on it is derived from a price table held in ai-service configuration, and no endpoint
 * exposes that table for editing.
 */
export function useOrganizationSpendReport() {
    return useQuery({
        queryKey: organizationSpendQueryKey,
        queryFn: () => apiClient.get<OrganizationSpendReport>("/admin/ai-usage"),
        staleTime: TimingConstants.thirtySecondsMs,
    });
}

/**
 * `PUT /admin/ai-quota`. The body never carries an organization id — sending one would be a
 * tenancy-boundary violation (`scripts/tenancy-boundary-lint.py`) and ai-service would ignore it
 * anyway. It writes to whichever organization the caller's token is scoped to.
 */
export function useSaveOrganizationQuota() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (writeModel: OrganizationQuotaWriteModel) =>
            apiClient.put<OrganizationQuotaSettings>("/admin/ai-quota", writeModel),
        onSuccess: (settings) => {
            clientLogger.warn("Organization AI quota saved", {
                llmMonthlyTokenLimit: settings.effectiveLlmMonthlyTokenLimit,
                batchReservePercent: settings.effectiveBatchReservePercent,
            });
            queryClient.invalidateQueries({ queryKey: organizationQuotaQueryKey });
            queryClient.invalidateQueries({ queryKey: organizationSpendQueryKey });
        },
        onError: (error) => {
            clientLogger.error("Failed to save the organization AI quota", {
                error: (error as Error).message,
            });
        },
    });
}

/** `GET /organizations/{id}` — the registry row, so the heading can name the organization in the URL. */
export function usePlatformOrganizationDetail(organizationId: string) {
    return useQuery({
        queryKey: ["admin", "organizations", organizationId],
        queryFn: () => apiClient.get<PlatformOrganizationDetail>(`/organizations/${organizationId}`),
        enabled: organizationId.length > 0,
    });
}
