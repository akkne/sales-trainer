import type {
    TeamSkillMap,
    TeamSkillMapCell,
} from "@/features/org-shell/hooks/use-team-directory";
import {
    MEMBERSHIP_STATUSES,
    type OrganizationMembership,
} from "@/features/org-team/types/organization-membership";

export const UNNAMED_MEMBER_LABEL = "Без имени";

export interface TeamHeatMapRow {
    userId: string;
    displayName: string;
    /// `null` means «не смогли проверить», which is a different statement from «работает» and is
    /// never drawn as one (ADMIN_UI_DESIGN.md O1, the `rosterKnown: false` state).
    isActiveMember: boolean | null;
    attemptCount: number;
    accuracyPercent: number | null;
    weakestStageKey: string | null;
    dialogCount: number;
    dialogAverageScore: number | null;
    stages: TeamSkillMapCell[];
    skills: TeamSkillMapCell[];
    /// Whether this person did anything at all inside the window. False is the first-run case, not
    /// an edge case.
    hasPractice: boolean;
}

export interface MergedTeamRoster {
    rows: TeamHeatMapRow[];
    /// Whether anybody — learning-service internally, or identity-service directly — could say who
    /// still works here. Only when this is false does the screen disclaim its «уже не работает» marks.
    isRosterKnown: boolean;
    /// People on the roster who practised nothing in the window. They are the rows the РОП most
    /// wants and the ones a practice-derived list cannot contain.
    silentMemberCount: number;
}

function resolveMembershipActivity(membership: OrganizationMembership): boolean {
    return membership.status === MEMBERSHIP_STATUSES.active;
}

function compareRows(left: TeamHeatMapRow, right: TeamHeatMapRow): number {
    const isLeftDeparted = left.isActiveMember === false;
    const isRightDeparted = right.isActiveMember === false;
    if (isLeftDeparted !== isRightDeparted) return isLeftDeparted ? 1 : -1;

    const leftActivity = left.attemptCount + left.dialogCount;
    const rightActivity = right.attemptCount + right.dialogCount;
    if (leftActivity !== rightActivity) return rightActivity - leftActivity;

    return left.displayName.localeCompare(right.displayName, "ru");
}

/// The heat map's rows: everyone learning-service measured, plus everyone identity-service says
/// works here, minus nobody.
///
/// `GET /admin/team/skill-map` cannot produce the design's «Кузьма О.† — уже не работает» row on
/// its own. When it reaches identity-service its member list *is* the active roster, so every
/// `isActiveMember` it returns is `true`; when it cannot, every one is `null`. Reading
/// `GET /memberships?status=all` from the panel — a route that landed after ADMIN_UI_DESIGN.md §6.1
/// was written — restores both halves: the departed people who still have history, and the hired
/// people who have practised nothing yet.
///
/// A missing roster is not an error here. Pass `null` and the function degrades to exactly what the
/// design specifies for the palliative case: whatever the map knew, with the honest `null`s intact.
export function mergeTeamRoster(
    skillMap: TeamSkillMap,
    roster: OrganizationMembership[] | null
): MergedTeamRoster {
    const membershipsByUserId = new Map(
        (roster ?? []).map((membership) => [membership.userId, membership])
    );

    const rows: TeamHeatMapRow[] = skillMap.members.map((member) => {
        const membership = membershipsByUserId.get(member.userId);
        const isActiveMember =
            roster === null
                ? member.isActiveMember
                : membership
                  ? resolveMembershipActivity(membership)
                  : null;

        return {
            userId: member.userId,
            displayName: member.displayName || membership?.displayName || UNNAMED_MEMBER_LABEL,
            isActiveMember,
            attemptCount: member.attemptCount,
            accuracyPercent: member.accuracyPercent,
            weakestStageKey: member.weakestStageKey,
            dialogCount: member.dialogCount,
            dialogAverageScore: member.dialogAverageScore,
            stages: member.stages,
            skills: member.skills,
            hasPractice: member.attemptCount > 0 || member.dialogCount > 0,
        };
    });

    const measuredUserIds = new Set(skillMap.members.map((member) => member.userId));

    for (const membership of roster ?? []) {
        if (measuredUserIds.has(membership.userId)) continue;
        if (!resolveMembershipActivity(membership)) continue;

        rows.push({
            userId: membership.userId,
            displayName: membership.displayName || UNNAMED_MEMBER_LABEL,
            isActiveMember: true,
            attemptCount: 0,
            accuracyPercent: null,
            weakestStageKey: null,
            dialogCount: 0,
            dialogAverageScore: null,
            stages: [],
            skills: [],
            hasPractice: false,
        });
    }

    rows.sort(compareRows);

    return {
        rows,
        isRosterKnown: roster !== null || skillMap.rosterKnown,
        silentMemberCount: rows.filter((row) => !row.hasPractice).length,
    };
}
