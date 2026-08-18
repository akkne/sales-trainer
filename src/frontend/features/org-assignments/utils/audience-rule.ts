import type {
    AssignmentAudience,
    AssignmentAudienceKind,
} from "@/features/org-assignments/types/assignment";

/**
 * The audience is a rule, not a list (docs/TENANCY/ASSIGNMENTS.md §2). «Вся команда» stays true as
 * people join; a list of user ids is a photograph of one afternoon. Both are legal, and the screen
 * has to send whichever one the РОП actually chose rather than resolving it into names itself.
 */
export function buildAudienceRule(
    kind: AssignmentAudienceKind,
    selectedUserIds: string[]
): AssignmentAudience {
    if (kind === "users") {
        return { kind: "users", userIds: Array.from(new Set(selectedUserIds)) };
    }

    return { kind };
}

/** The refusal to show under the audience section, or null when it can be sent. */
export function validateAudienceRule(audience: AssignmentAudience): string | null {
    if (audience.kind === "users" && (audience.userIds ?? []).length === 0) {
        return "Выберите хотя бы одного человека — или выдайте задание всей команде.";
    }

    if (audience.kind === "group" && !audience.groupId) {
        return "Группа не выбрана.";
    }

    return null;
}

export function pluralizePeople(count: number): string {
    const lastTwoDigits = count % 100;
    const lastDigit = count % 10;
    if (lastTwoDigits >= 11 && lastTwoDigits <= 14) return "человек";
    if (lastDigit === 1) return "человек";
    if (lastDigit >= 2 && lastDigit <= 4) return "человека";
    return "человек";
}

/**
 * The audience in the words of the list row.
 *
 * `AssignmentSummaryDto` carries only `audienceKind` — never the chosen user ids — so the headcount
 * of a `users` audience can only be borrowed from the resolved progress rows, which a draft does not
 * have yet. When there is no honest number the row says «выбранные люди» rather than «0 человек».
 */
export function describeAudienceKind(
    audienceKind: string,
    resolvedPersonCount: number | null = null
): string {
    if (audienceKind === "whole_team") return "вся команда";
    if (audienceKind === "group") return "группа";

    if (audienceKind === "users") {
        return resolvedPersonCount !== null && resolvedPersonCount > 0
            ? `${resolvedPersonCount} ${pluralizePeople(resolvedPersonCount)}`
            : "выбранные люди";
    }

    return audienceKind;
}

/** The audience of a fully loaded assignment, where the chosen ids are actually present. */
export function describeAudience(audience: AssignmentAudience): string {
    if (audience.kind === "users") {
        const chosenCount = (audience.userIds ?? []).length;
        return `${chosenCount} ${pluralizePeople(chosenCount)}`;
    }

    return describeAudienceKind(audience.kind);
}

export function pluralizeContentItems(count: number): string {
    const lastTwoDigits = count % 100;
    const lastDigit = count % 10;
    if (lastTwoDigits >= 11 && lastTwoDigits <= 14) return "материалов";
    if (lastDigit === 1) return "материал";
    if (lastDigit >= 2 && lastDigit <= 4) return "материала";
    return "материалов";
}
