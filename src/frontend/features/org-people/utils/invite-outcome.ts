import type { CreateInvitesResponse } from "@/features/org-people/types/organization-people";

export interface InviteOutcomeLine {
    email: string;
    wasCreated: boolean;
    inviteId: string | null;
    expiresAt: string | null;
    rejectionReason: string | null;
}

export interface InviteOutcomeSummary {
    createdCount: number;
    rejectedCount: number;
    isPartial: boolean;
}

function normalizeEmail(email: string): string {
    return email.trim().toLowerCase();
}

/// One list out of the two halves of `CreateInvitesResponse`, back in the order the addresses were
/// submitted in.
///
/// The response splits the answer into `created` and `rejected`, but the person reading it pasted
/// one list and is scanning for their own lines — «третья строка не прошла» is the question, and
/// two separate blocks make it unanswerable. Matching is on the trimmed lower-cased address because
/// the server normalizes what it accepts and echoes back verbatim what it could not parse.
///
/// Anything the submitted list does not account for is appended rather than dropped: an answer
/// mentioning an address nobody asked about is a bug worth seeing, not one worth hiding.
export function buildInviteOutcomeLines(
    response: CreateInvitesResponse,
    submittedEmails: string[]
): InviteOutcomeLine[] {
    const submissionOrderByEmail = new Map<string, number>();
    submittedEmails.forEach((submittedEmail, submissionIndex) => {
        const normalized = normalizeEmail(submittedEmail);
        if (!submissionOrderByEmail.has(normalized)) {
            submissionOrderByEmail.set(normalized, submissionIndex);
        }
    });

    const lines: InviteOutcomeLine[] = [
        ...response.created.map((createdInvite) => ({
            email: createdInvite.email,
            wasCreated: true,
            inviteId: createdInvite.id,
            expiresAt: createdInvite.expiresAt,
            rejectionReason: null,
        })),
        ...response.rejected.map((rejectedInvite) => ({
            email: rejectedInvite.email,
            wasCreated: false,
            inviteId: null,
            expiresAt: null,
            rejectionReason: rejectedInvite.reason,
        })),
    ];

    return lines
        .map((line, positionInResponse) => ({ line, positionInResponse }))
        .sort((first, second) => {
            const firstOrder =
                submissionOrderByEmail.get(normalizeEmail(first.line.email)) ??
                Number.MAX_SAFE_INTEGER;
            const secondOrder =
                submissionOrderByEmail.get(normalizeEmail(second.line.email)) ??
                Number.MAX_SAFE_INTEGER;
            if (firstOrder !== secondOrder) return firstOrder - secondOrder;
            return first.positionInResponse - second.positionInResponse;
        })
        .map((entry) => entry.line);
}

/// A bulk invite where three of forty failed is the ordinary case, not an error — the summary says
/// both numbers and never collapses into «готово» or into «ошибка».
export function summarizeInviteOutcome(response: CreateInvitesResponse): InviteOutcomeSummary {
    return {
        createdCount: response.created.length,
        rejectedCount: response.rejected.length,
        isPartial: response.created.length > 0 && response.rejected.length > 0,
    };
}

export function describeInviteOutcome(summary: InviteOutcomeSummary): string {
    if (summary.createdCount === 0) {
        return `Ни одно приглашение не отправлено · отклонено адресов: ${summary.rejectedCount}`;
    }
    if (summary.rejectedCount === 0) {
        return `Отправлено приглашений: ${summary.createdCount}`;
    }
    return `Отправлено приглашений: ${summary.createdCount} · отклонено адресов: ${summary.rejectedCount}`;
}
