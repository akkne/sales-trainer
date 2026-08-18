import { UNNAMED_MEMBER_LABEL } from "@/features/org-dialogs/constants/dialog-review-dictionary";
import type { TeamMemberName } from "@/features/org-shell/hooks/use-team-directory";

/**
 * O5 and O6 list conversations across a team, and ai-service holds no user replica — the summary
 * DTO carries a `userId` and deliberately no name (`AdminDialogSessionSummaryDto`). The names come
 * from the heat map the panel has already read, through `useTeamMemberNames`
 * (docs/TENANCY/ADMIN_UI_DESIGN.md O5).
 *
 * Somebody the directory does not know still gets a row. The heat map only knows people who have
 * attempted something, so «no name» is an ordinary case here, not an error — and it must stay
 * distinguishable per person, which is why the identifier's head is part of the label.
 */

const UNNAMED_IDENTIFIER_LENGTH = 8;

export function buildMemberNamesByUserId(memberNames: TeamMemberName[]): Map<string, string> {
    return new Map(memberNames.map((member) => [member.userId, member.displayName]));
}

export function resolveMemberLabel(
    userId: string,
    memberNamesByUserId: Map<string, string>
): string {
    const displayName = memberNamesByUserId.get(userId);
    if (displayName) return displayName;
    return `${UNNAMED_MEMBER_LABEL} · ${userId.slice(0, UNNAMED_IDENTIFIER_LENGTH)}`;
}

/**
 * The name a note thread shows for one side of it. A note's own DTO carries the display names
 * learning-service resolved, so the directory is only a fallback here — and «Вы» wins over both,
 * because the point of the label is telling the two sides of the argument apart.
 */
export function resolveNoteParticipantLabel(
    userId: string,
    storedDisplayName: string | null,
    currentUserId: string | null,
    currentUserLabel: string,
    memberNamesByUserId: Map<string, string>
): string {
    if (currentUserId !== null && userId === currentUserId) return currentUserLabel;
    if (storedDisplayName) return storedDisplayName;
    return resolveMemberLabel(userId, memberNamesByUserId);
}
