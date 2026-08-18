/**
 * The wire shapes of `organizations/profile` (organization-service,
 * `Features/Organizations/Models/*`), transcribed field for field. They are declared here rather
 * than derived from anything, for the same reason the backend redeclares its draft record instead
 * of sharing learning-service's: this is a public contract, and a type that follows another
 * feature's refactor is a screen that breaks without a diff.
 */

export interface OrganizationObjection {
    text: string;
    /** How often it comes up. Only ever written by a human — extraction never fills it in. */
    frequency: string | null;
    bestResponse: string | null;
}

export interface OrganizationProfile {
    product: string | null;
    icp: string | null;
    objections: OrganizationObjection[];
    scriptStages: string[];
    tone: string | null;
    glossary: Record<string, string>;
    bannedClaims: string[];
    createdAt: string;
    updatedAt: string;
}

export interface OrganizationProfileGap {
    /** One of `ORGANIZATION_PROFILE_FIELDS`; the screen keys off this, never off the sentence. */
    code: string;
    question: string;
    /** `blocking` | `important` | `optional`. */
    priority: string;
}

export interface OrganizationProfileGaps {
    questions: OrganizationProfileGap[];
    /** Every gap, including the ones this round did not return. */
    totalGapCount: number;
    blockingGapCount: number;
    /**
     * True once no `blocking` gap is left — the lessons stop saying «ваш продукт». Deliberately a
     * narrower claim than «профиль заполнен» (docs/CONTENT_PARAMETERIZATION.md §2.1).
     */
    isReadyForParameterization: boolean;
}

export interface ExtractedProfileObjection {
    text: string;
    bestResponse: string | null;
}

export interface ExtractedProfileDraft {
    product?: string | null;
    icp?: string | null;
    tone?: string | null;
    objections?: ExtractedProfileObjection[] | null;
    scriptStages?: string[] | null;
    glossary?: Record<string, string> | null;
    bannedClaims?: string[] | null;
}

export interface OrganizationProfileFieldProposal {
    field: string;
    /** One of `PROFILE_DRAFT_DECISIONS`. */
    decision: string;
    /** Only ever set on the four single-valued fields. */
    currentValue: string | null;
    suggestedValue: string | null;
    /** How many entries an additive field would gain. Zero everywhere else. */
    addedItemCount: number;
}

export interface OrganizationProfileDraftPreview {
    fields: OrganizationProfileFieldProposal[];
    conflictCount: number;
    gapsAfterApply: OrganizationProfileGaps;
}

export interface OrganizationProfileDraftApplied {
    profile: OrganizationProfile;
    appliedFields: OrganizationProfileFieldProposal[];
    /** The next round of the interview, so applying a draft costs no second request. */
    gaps: OrganizationProfileGaps;
}

/** Whole-row replace: an omitted field is cleared, not kept. */
export interface UpdateOrganizationProfileRequest {
    product: string | null;
    icp: string | null;
    objections: OrganizationObjection[];
    scriptStages: string[];
    tone: string | null;
    glossary: Record<string, string>;
    bannedClaims: string[];
}

/**
 * One answer to one question. An omitted field keeps its stored value, which is the only reason
 * two people can answer the interview at the same time without overwriting each other.
 */
export type PatchOrganizationProfileRequest = Partial<UpdateOrganizationProfileRequest>;

export interface ApplyOrganizationProfileDraftRequest {
    draft: ExtractedProfileDraft;
    /** Only names from `OVERWRITABLE_PROFILE_FIELDS` mean anything; the server drops the rest. */
    acceptedFields: string[];
}
