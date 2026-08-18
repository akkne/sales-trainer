import { ApiError, apiClient } from "@/shared/api/api-client";
import type {
    ApplyOrganizationProfileDraftRequest,
    ExtractedProfileDraft,
    OrganizationProfile,
    OrganizationProfileDraftApplied,
    OrganizationProfileDraftPreview,
    OrganizationProfileGaps,
    PatchOrganizationProfileRequest,
    UpdateOrganizationProfileRequest,
} from "../types/organization-profile";
import { PROFILE_QUESTION_LIMIT } from "../constants/profile-fields";

const ORGANIZATION_PROFILE_ROUTES = {
    profile: "/organizations/profile",
    gaps: (limit: number) => `/organizations/profile/gaps?limit=${limit}`,
    draft: "/organizations/profile/draft",
    draftApply: "/organizations/profile/draft/apply",
} as const;

/**
 * Every call the company-profile screen makes. Four of the six routes are gated `RequireOrgAdmin`
 * on the controller — reading the profile and reading the gaps are open to any member on purpose,
 * so that a rep can see what the company profile says about them.
 */
export const organizationProfileService = {
    /**
     * `null` rather than a thrown error on 404. An organization that has never saved a profile is
     * the first-run case this whole screen exists for, and «не найдено» is the least useful thing
     * to tell somebody about it.
     */
    async getProfile(): Promise<OrganizationProfile | null> {
        try {
            return await apiClient.get<OrganizationProfile>(ORGANIZATION_PROFILE_ROUTES.profile);
        } catch (error) {
            if (error instanceof ApiError && error.status === 404) return null;
            throw error;
        }
    },

    getGaps(limit: number = PROFILE_QUESTION_LIMIT): Promise<OrganizationProfileGaps> {
        return apiClient.get<OrganizationProfileGaps>(ORGANIZATION_PROFILE_ROUTES.gaps(limit));
    },

    /** One answer to one question. Omitted fields keep whatever a colleague saved meanwhile. */
    patchProfile(request: PatchOrganizationProfileRequest): Promise<OrganizationProfile> {
        return apiClient.patch<OrganizationProfile>(ORGANIZATION_PROFILE_ROUTES.profile, request);
    },

    /** Whole-row replace. The only path that can shorten `bannedClaims`. */
    replaceProfile(request: UpdateOrganizationProfileRequest): Promise<OrganizationProfile> {
        return apiClient.put<OrganizationProfile>(ORGANIZATION_PROFILE_ROUTES.profile, request);
    },

    previewDraft(draft: ExtractedProfileDraft): Promise<OrganizationProfileDraftPreview> {
        return apiClient.post<OrganizationProfileDraftPreview>(
            ORGANIZATION_PROFILE_ROUTES.draft,
            draft
        );
    },

    applyDraft(
        request: ApplyOrganizationProfileDraftRequest
    ): Promise<OrganizationProfileDraftApplied> {
        return apiClient.post<OrganizationProfileDraftApplied>(
            ORGANIZATION_PROFILE_ROUTES.draftApply,
            request
        );
    },
};
