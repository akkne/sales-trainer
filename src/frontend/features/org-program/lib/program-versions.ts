import type { ProgramEnrollment, ProgramVersionSummary } from "../types/program";
import type { ProgramRosterMember } from "../types/program-roster";

const PUBLISHED_STATUS = "published";
const DRAFT_STATUS = "draft";

/** Newest version number first, whatever order the endpoint happened to answer in. */
function byVersionNumberDescending(
    left: ProgramVersionSummary,
    right: ProgramVersionSummary
): number {
    return right.versionNumber - left.versionNumber;
}

/** The organization's single mutable draft, or `null`. At most one can exist by construction. */
export function selectDraftVersion(
    versions: readonly ProgramVersionSummary[]
): ProgramVersionSummary | null {
    return versions.find((version) => version.status === DRAFT_STATUS) ?? null;
}

export function selectPublishedVersions(
    versions: readonly ProgramVersionSummary[]
): ProgramVersionSummary[] {
    return versions
        .filter((version) => version.status === PUBLISHED_STATUS)
        .sort(byVersionNumberDescending);
}

/**
 * The version a newly enrolled person lands on — the newest **published** one. A draft is never it:
 * a draft is still being edited and nobody can be pinned to it.
 */
export function selectCurrentPublishedVersion(
    versions: readonly ProgramVersionSummary[]
): ProgramVersionSummary | null {
    return selectPublishedVersions(versions)[0] ?? null;
}

/**
 * The published version immediately before the given one, which is the baseline «Что изменилось»
 * compares against. `null` for the very first published version — there is nothing to diff it with,
 * and the button must be absent rather than disabled-with-an-error.
 */
export function selectPreviousPublishedVersion(
    versions: readonly ProgramVersionSummary[],
    programVersionId: string
): ProgramVersionSummary | null {
    const published = selectPublishedVersions(versions);
    const position = published.findIndex((version) => version.id === programVersionId);
    if (position < 0) return null;

    return published[position + 1] ?? null;
}

/**
 * Whether this person is still learning an older programme than the one being handed out today.
 *
 * Compared by version **id**, not by version number: the id is what the pin actually stores, and a
 * number is only a label the two lists agree on by convention. With no published version at all
 * nobody is behind — the whole team is on the live tree, which is a different statement.
 */
export function isEnrollmentBehind(
    enrollment: ProgramEnrollment,
    currentPublishedVersion: ProgramVersionSummary | null
): boolean {
    if (currentPublishedVersion === null) return false;
    return enrollment.programVersionId !== currentPublishedVersion.id;
}

export interface ProgramVersionEnrollmentGroup {
    programVersionId: string;
    programVersionNumber: number;
    enrollmentCount: number;
    isCurrentPublishedVersion: boolean;
}

export interface EnrollmentSpread {
    enrolledCount: number;
    onCurrentVersionCount: number;
    behindCount: number;
    /** One entry per version somebody is actually pinned to, newest number first. */
    groups: ProgramVersionEnrollmentGroup[];
    /** True as soon as two versions are in use at once — the normal state after the first change. */
    isSpreadAcrossVersions: boolean;
    /** People the roster knows who hold no pin at all; they are on the live tree, not on any version. */
    notEnrolledCount: number;
}

export interface EnrollmentSpreadInput {
    enrollments: readonly ProgramEnrollment[];
    currentPublishedVersion: ProgramVersionSummary | null;
    rosterMembers: readonly ProgramRosterMember[];
}

/**
 * The one paragraph of arithmetic the screen exists to get right: how many people are on today's
 * version, how many are behind and on what, and how many hold no pin at all.
 *
 * The last number is the one a reader most easily misses. Somebody who was never enrolled is not
 * «on the latest version» — they are on the live tree and see every edit immediately, which is the
 * opposite guarantee.
 */
export function summarizeEnrollmentSpread({
    enrollments,
    currentPublishedVersion,
    rosterMembers,
}: EnrollmentSpreadInput): EnrollmentSpread {
    const groupsByVersionId = new Map<string, ProgramVersionEnrollmentGroup>();

    for (const enrollment of enrollments) {
        const existingGroup = groupsByVersionId.get(enrollment.programVersionId);
        if (existingGroup) {
            existingGroup.enrollmentCount += 1;
            continue;
        }

        groupsByVersionId.set(enrollment.programVersionId, {
            programVersionId: enrollment.programVersionId,
            programVersionNumber: enrollment.programVersionNumber,
            enrollmentCount: 1,
            isCurrentPublishedVersion:
                currentPublishedVersion !== null &&
                enrollment.programVersionId === currentPublishedVersion.id,
        });
    }

    const groups = [...groupsByVersionId.values()].sort(
        (left, right) => right.programVersionNumber - left.programVersionNumber
    );

    const behindCount = enrollments.filter((enrollment) =>
        isEnrollmentBehind(enrollment, currentPublishedVersion)
    ).length;

    const enrolledUserIds = new Set(enrollments.map((enrollment) => enrollment.userId));

    return {
        enrolledCount: enrollments.length,
        onCurrentVersionCount:
            currentPublishedVersion === null ? 0 : enrollments.length - behindCount,
        behindCount,
        groups,
        isSpreadAcrossVersions: groups.length > 1,
        notEnrolledCount: rosterMembers.filter((member) => !enrolledUserIds.has(member.userId))
            .length,
    };
}

/** The people «Зачислить ещё» may offer: everybody the roster knows who holds no pin yet. */
export function selectEnrollableMembers(
    rosterMembers: readonly ProgramRosterMember[],
    enrollments: readonly ProgramEnrollment[]
): ProgramRosterMember[] {
    const enrolledUserIds = new Set(enrollments.map((enrollment) => enrollment.userId));
    return rosterMembers.filter((member) => !enrolledUserIds.has(member.userId));
}

export function buildMemberNameLookup(
    rosterMembers: readonly ProgramRosterMember[]
): Map<string, string> {
    return new Map(rosterMembers.map((member) => [member.userId, member.displayName]));
}
