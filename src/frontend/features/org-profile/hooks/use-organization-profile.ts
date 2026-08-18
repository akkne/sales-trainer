"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/api-client";
import { clientLogger } from "@/shared/utils/client-logger";
import { organizationProfileService } from "../services/organization-profile-service";
import { profileMaterialRunService } from "../services/profile-material-run-service";
import type {
    ApplyOrganizationProfileDraftRequest,
    ExtractedProfileDraft,
    OrganizationProfile,
    OrganizationProfileDraftApplied,
    OrganizationProfileGaps,
    PatchOrganizationProfileRequest,
    UpdateOrganizationProfileRequest,
} from "../types/organization-profile";

const ORGANIZATION_PROFILE_QUERY_KEY = ["org", "profile"];
const ORGANIZATION_PROFILE_GAPS_QUERY_KEY = ["org", "profile", "gaps"];

const FORBIDDEN_MESSAGE =
    "Изменять профиль компании может только администратор организации. Ваш доступ — только на чтение.";
const GENERIC_SAVE_FAILURE_MESSAGE = "Не удалось сохранить. Попробуйте ещё раз.";

/**
 * A save that failed, in a sentence the РОП can act on. A 403 here is not a bug to retry — the read
 * routes of this controller are open to every member and the write routes are not, so somebody who
 * can see the screen can legitimately be unable to change it.
 */
export function describeProfileWriteFailure(error: unknown): string {
    if (error instanceof ApiError) {
        if (error.status === 403) return FORBIDDEN_MESSAGE;
        if (typeof error.payload.message === "string" && error.payload.message.length > 0) {
            return error.payload.message;
        }
    }
    return GENERIC_SAVE_FAILURE_MESSAGE;
}

/** The stored profile, or `null` when the organization has never saved one. 404 is not an error. */
export function useOrganizationProfile() {
    return useQuery<OrganizationProfile | null>({
        queryKey: ORGANIZATION_PROFILE_QUERY_KEY,
        queryFn: () => organizationProfileService.getProfile(),
    });
}

/** The next round of the interview. Answers 200 with an empty list, never 404. */
export function useOrganizationProfileGaps() {
    return useQuery<OrganizationProfileGaps>({
        queryKey: ORGANIZATION_PROFILE_GAPS_QUERY_KEY,
        queryFn: () => organizationProfileService.getGaps(),
    });
}

/**
 * One answer. Both the profile and the gap list are refetched afterwards, because the next question
 * depends on the answer that just landed — and because a colleague may have answered a different one
 * in the meantime.
 */
export function useAnswerProfileQuestion() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (request: PatchOrganizationProfileRequest) =>
            organizationProfileService.patchProfile(request),
        onSuccess: (profile, request) => {
            clientLogger.info("Organization profile question answered", {
                fields: Object.keys(request),
            });
            queryClient.setQueryData(ORGANIZATION_PROFILE_QUERY_KEY, profile);
            queryClient.invalidateQueries({ queryKey: ORGANIZATION_PROFILE_GAPS_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to answer organization profile question", {
                error: (error as Error).message,
            });
        },
    });
}

/** The whole-row save behind «Показать все поля профиля». */
export function useReplaceOrganizationProfile() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (request: UpdateOrganizationProfileRequest) =>
            organizationProfileService.replaceProfile(request),
        onSuccess: (profile) => {
            clientLogger.info("Organization profile replaced", {
                bannedClaimCount: profile.bannedClaims.length,
            });
            queryClient.setQueryData(ORGANIZATION_PROFILE_QUERY_KEY, profile);
            queryClient.invalidateQueries({ queryKey: ORGANIZATION_PROFILE_GAPS_QUERY_KEY });
        },
        onError: (error) => {
            clientLogger.error("Failed to replace organization profile", {
                error: (error as Error).message,
            });
        },
    });
}

/** What promoting an extracted structure would do. Writes nothing. */
export function usePreviewProfileDraft() {
    return useMutation({
        mutationFn: (draft: ExtractedProfileDraft) =>
            organizationProfileService.previewDraft(draft),
        onError: (error) => {
            clientLogger.error("Failed to preview organization profile draft", {
                error: (error as Error).message,
            });
        },
    });
}

/**
 * Promotes the reviewed draft. The response carries the next round of questions with it, so the
 * screen turns straight back into the interview without a second request and without a spinner
 * between «ИИ заполнил профиль» and «остался один вопрос».
 */
export function useApplyProfileDraft() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (request: ApplyOrganizationProfileDraftRequest) =>
            organizationProfileService.applyDraft(request),
        onSuccess: (applied: OrganizationProfileDraftApplied) => {
            clientLogger.info("Organization profile draft applied", {
                appliedFields: applied.appliedFields.map((proposal) => proposal.field),
            });
            queryClient.setQueryData(ORGANIZATION_PROFILE_QUERY_KEY, applied.profile);
            queryClient.setQueryData(ORGANIZATION_PROFILE_GAPS_QUERY_KEY, applied.gaps);
        },
        onError: (error) => {
            clientLogger.error("Failed to apply organization profile draft", {
                error: (error as Error).message,
            });
        },
    });
}

/**
 * Starts a 40.27 pipeline run from pasted material. The profile is filled in from its checkpoint,
 * not from here — this screen only opens the run.
 */
export function useStartMaterialRun() {
    return useMutation({
        mutationFn: ({ title, material }: { title: string; material: string }) =>
            profileMaterialRunService.startRun(title, material),
        onError: (error) => {
            clientLogger.error("Failed to start content generation run from profile screen", {
                error: (error as Error).message,
            });
        },
    });
}
