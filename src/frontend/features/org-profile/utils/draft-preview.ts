import {
    ORGANIZATION_PROFILE_FIELD_ORDER,
    OVERWRITABLE_PROFILE_FIELDS,
    PROFILE_DRAFT_DECISIONS,
} from "../constants/profile-fields";
import type { OrganizationProfileFieldProposal } from "../types/organization-profile";

export interface DraftProposalGroups {
    /** Fields that were empty and the draft can fill. Applied without asking. */
    filled: OrganizationProfileFieldProposal[];
    /** Lists that only grow. Applied without asking, because nothing is lost. */
    extended: OrganizationProfileFieldProposal[];
    /** Both sides have a value and they differ. Nothing happens unless a person says so. */
    conflicting: OrganizationProfileFieldProposal[];
}

const byInterviewOrder = (
    first: OrganizationProfileFieldProposal,
    second: OrganizationProfileFieldProposal
): number =>
    ORGANIZATION_PROFILE_FIELD_ORDER.indexOf(first.field) -
    ORGANIZATION_PROFILE_FIELD_ORDER.indexOf(second.field);

/**
 * The preview's three sections. `unchanged` proposals are dropped rather than rendered greyed out:
 * a list of things that will not happen is noise on a screen whose only job is to show what will.
 */
export function groupDraftProposals(
    proposals: readonly OrganizationProfileFieldProposal[]
): DraftProposalGroups {
    return {
        filled: proposals
            .filter((proposal) => proposal.decision === PROFILE_DRAFT_DECISIONS.fill)
            .sort(byInterviewOrder),
        extended: proposals
            .filter(
                (proposal) =>
                    proposal.decision === PROFILE_DRAFT_DECISIONS.extend &&
                    proposal.addedItemCount > 0
            )
            .sort(byInterviewOrder),
        conflicting: proposals
            .filter((proposal) => proposal.decision === PROFILE_DRAFT_DECISIONS.conflict)
            .sort(byInterviewOrder),
    };
}

/**
 * Ticking or unticking one conflict.
 *
 * A field the server would ignore is never added to the list, so the screen cannot promise a
 * replacement that will not happen — most importantly for `banned_claims`, which is absent from
 * `OrganizationProfileFields.Overwritable` precisely so that no client can delete a compliance rule
 * by naming it here.
 */
export function toggleAcceptedField(
    acceptedFields: readonly string[],
    field: string
): string[] {
    if (!OVERWRITABLE_PROFILE_FIELDS.includes(field)) return [...acceptedFields];
    if (acceptedFields.includes(field)) {
        return acceptedFields.filter((accepted) => accepted !== field);
    }
    return [...acceptedFields, field];
}

/**
 * True when applying the draft changes nothing at all — every proposal came back `unchanged`. The
 * screen says so instead of offering a button that would write the profile it already has.
 */
export function isDraftWithoutEffect(
    proposals: readonly OrganizationProfileFieldProposal[]
): boolean {
    const groups = groupDraftProposals(proposals);
    return (
        groups.filled.length === 0 &&
        groups.extended.length === 0 &&
        groups.conflicting.length === 0
    );
}
