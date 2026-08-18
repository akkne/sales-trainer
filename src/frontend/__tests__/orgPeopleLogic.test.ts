import { describe, expect, it } from "vitest";
import {
    INVITE_REJECTION_LABELS,
    INVITE_STATUS_LABELS,
    describeInviteRejection,
    describeInviteStatus,
    describeMembershipStatus,
    describeOrganizationRole,
    INVITABLE_ORGANIZATION_ROLES,
} from "@/features/org-people/constants/people-dictionary";
import type { CreateInvitesResponse } from "@/features/org-people/types/organization-people";
import {
    buildMemberInitials,
    describeMemberName,
    formatLongRussianDate,
    formatShortRussianDate,
} from "@/features/org-people/utils/format-people";
import { parseInviteEmails } from "@/features/org-people/utils/invite-emails";
import {
    buildInviteOutcomeLines,
    describeInviteOutcome,
    summarizeInviteOutcome,
} from "@/features/org-people/utils/invite-outcome";

describe("parseInviteEmails", () => {
    it("splits on newlines, commas and semicolons at once", () => {
        const emails = parseInviteEmails(
            "ivanov@acme.ru\npetrov@acme.ru, sokolova@acme.ru; orlov@acme.ru"
        );

        expect(emails).toEqual([
            "ivanov@acme.ru",
            "petrov@acme.ru",
            "sokolova@acme.ru",
            "orlov@acme.ru",
        ]);
    });

    it("drops blank lines and trailing separators rather than sending empty addresses", () => {
        expect(parseInviteEmails("\n\n ivanov@acme.ru ,,\n  \n")).toEqual(["ivanov@acme.ru"]);
    });

    it("returns nothing for an empty or whitespace-only paste", () => {
        expect(parseInviteEmails("")).toEqual([]);
        expect(parseInviteEmails("   \n  ")).toEqual([]);
    });

    it("keeps duplicates and original casing — the server names them, the client does not hide them", () => {
        expect(parseInviteEmails("Ivanov@acme.ru\nivanov@acme.ru")).toEqual([
            "Ivanov@acme.ru",
            "ivanov@acme.ru",
        ]);
    });
});

describe("the invite status vocabulary", () => {
    it("names all four server-derived states, and they read as four different things", () => {
        expect(describeInviteStatus("pending")).toBe("Ждёт ответа");
        expect(describeInviteStatus("accepted")).toBe("Принято");
        expect(describeInviteStatus("revoked")).toBe("Отозвано");
        expect(describeInviteStatus("expired")).toBe("Истекло");

        const labels = Object.values(INVITE_STATUS_LABELS);
        expect(new Set(labels).size).toBe(labels.length);
    });

    it("covers exactly the four states the backend derives, no more", () => {
        expect(Object.keys(INVITE_STATUS_LABELS).sort()).toEqual([
            "accepted",
            "expired",
            "pending",
            "revoked",
        ]);
    });

    it("shows an unknown status verbatim instead of guessing", () => {
        expect(describeInviteStatus("half-accepted")).toBe("half-accepted");
    });
});

describe("the rejection-reason vocabulary", () => {
    it("translates the four machine-readable reasons POST /invites can answer with", () => {
        expect(describeInviteRejection("invalid-email")).toBe("непохоже на адрес");
        expect(describeInviteRejection("duplicate-in-request")).toBe("повторяется в списке");
        expect(describeInviteRejection("already-a-member")).toBe("уже в компании");
        expect(describeInviteRejection("invite-already-pending")).toBe(
            "приглашение уже отправлено"
        );
    });

    it("covers exactly those four codes", () => {
        expect(Object.keys(INVITE_REJECTION_LABELS).sort()).toEqual([
            "already-a-member",
            "duplicate-in-request",
            "invalid-email",
            "invite-already-pending",
        ]);
    });

    it("shows an unrecognised reason verbatim rather than swallowing it", () => {
        expect(describeInviteRejection("mailbox-full")).toBe("mailbox-full");
    });
});

describe("role and membership-status vocabulary", () => {
    it("names the three organization roles", () => {
        expect(describeOrganizationRole("Manager")).toBe("Менеджер");
        expect(describeOrganizationRole("TenancyAdmin")).toBe("Администратор");
        expect(describeOrganizationRole("TenancySuperAdmin")).toBe("Суперадминистратор");
    });

    it("offers all three roles for invitation, because there is no route that re-roles a member", () => {
        expect([...INVITABLE_ORGANIZATION_ROLES]).toEqual([
            "Manager",
            "TenancyAdmin",
            "TenancySuperAdmin",
        ]);
    });

    it("does not know the retired OrgAdmin name — the backend rejects it too", () => {
        expect(describeOrganizationRole("OrgAdmin")).toBe("OrgAdmin");
    });

    it("says «Отключён», never «удалён»", () => {
        expect(describeMembershipStatus("Active")).toBe("Работает");
        expect(describeMembershipStatus("Deactivated")).toBe("Отключён");
        expect(describeMembershipStatus("Deactivated")).not.toContain("удал");
    });
});

describe("buildInviteOutcomeLines", () => {
    const submittedEmails = [
        "ivanov@acme.ru",
        "petrov@acme.ru",
        "sokolova@acme.ru",
        "не-адрес",
    ];

    const response: CreateInvitesResponse = {
        created: [
            {
                id: "invite-ivanov",
                email: "ivanov@acme.ru",
                role: "Manager",
                expiresAt: "2026-08-25T12:00:00Z",
            },
            {
                id: "invite-petrov",
                email: "petrov@acme.ru",
                role: "Manager",
                expiresAt: "2026-08-25T12:00:00Z",
            },
        ],
        rejected: [
            { email: "sokolova@acme.ru", reason: "already-a-member" },
            { email: "не-адрес", reason: "invalid-email" },
        ],
    };

    it("returns one list in the order the addresses were pasted, not two blocks", () => {
        const lines = buildInviteOutcomeLines(response, submittedEmails);

        expect(lines.map((line) => line.email)).toEqual(submittedEmails);
        expect(lines.map((line) => line.wasCreated)).toEqual([true, true, false, false]);
    });

    it("carries the invite id only on the created half — there is nothing to revoke on a refusal", () => {
        const lines = buildInviteOutcomeLines(response, submittedEmails);

        expect(lines[0].inviteId).toBe("invite-ivanov");
        expect(lines[2].inviteId).toBeNull();
        expect(lines[2].rejectionReason).toBe("already-a-member");
        expect(lines[0].rejectionReason).toBeNull();
    });

    it("matches server-normalized addresses back to what the person typed", () => {
        const lines = buildInviteOutcomeLines(
            {
                created: [
                    {
                        id: "invite-orlov",
                        email: "orlov@acme.ru",
                        role: "Manager",
                        expiresAt: "2026-08-25T12:00:00Z",
                    },
                ],
                rejected: [{ email: "ivanov@acme.ru", reason: "already-a-member" }],
            },
            ["  Ivanov@Acme.ru ", "ORLOV@acme.ru"]
        );

        expect(lines.map((line) => line.email)).toEqual(["ivanov@acme.ru", "orlov@acme.ru"]);
    });

    it("appends an address nobody submitted instead of dropping it", () => {
        const lines = buildInviteOutcomeLines(
            {
                created: [],
                rejected: [{ email: "ghost@acme.ru", reason: "invalid-email" }],
            },
            ["ivanov@acme.ru"]
        );

        expect(lines).toHaveLength(1);
        expect(lines[0].email).toBe("ghost@acme.ru");
    });

    it("keeps both halves of a duplicated address, created before refused", () => {
        const lines = buildInviteOutcomeLines(
            {
                created: [
                    {
                        id: "invite-petrov",
                        email: "petrov@acme.ru",
                        role: "Manager",
                        expiresAt: "2026-08-25T12:00:00Z",
                    },
                ],
                rejected: [{ email: "petrov@acme.ru", reason: "duplicate-in-request" }],
            },
            ["petrov@acme.ru", "petrov@acme.ru"]
        );

        expect(lines.map((line) => line.wasCreated)).toEqual([true, false]);
        expect(lines[1].rejectionReason).toBe("duplicate-in-request");
    });

    it("renders nothing when the response is empty on both sides", () => {
        expect(buildInviteOutcomeLines({ created: [], rejected: [] }, [])).toEqual([]);
    });
});

describe("summarizeInviteOutcome / describeInviteOutcome", () => {
    it("calls three refusals out of forty partial, and says both numbers", () => {
        const summary = summarizeInviteOutcome({
            created: Array.from({ length: 37 }, (_, inviteIndex) => ({
                id: `invite-${inviteIndex}`,
                email: `person${inviteIndex}@acme.ru`,
                role: "Manager",
                expiresAt: "2026-08-25T12:00:00Z",
            })),
            rejected: [
                { email: "a@acme.ru", reason: "invalid-email" },
                { email: "b@acme.ru", reason: "already-a-member" },
                { email: "c@acme.ru", reason: "duplicate-in-request" },
            ],
        });

        expect(summary).toEqual({ createdCount: 37, rejectedCount: 3, isPartial: true });
        expect(describeInviteOutcome(summary)).toBe(
            "Отправлено приглашений: 37 · отклонено адресов: 3"
        );
    });

    it("does not call an all-created answer partial", () => {
        const summary = summarizeInviteOutcome({
            created: [
                {
                    id: "invite-ivanov",
                    email: "ivanov@acme.ru",
                    role: "Manager",
                    expiresAt: "2026-08-25T12:00:00Z",
                },
            ],
            rejected: [],
        });

        expect(summary.isPartial).toBe(false);
        expect(describeInviteOutcome(summary)).toBe("Отправлено приглашений: 1");
    });

    it("says plainly that nothing went out when every address was refused", () => {
        const summary = summarizeInviteOutcome({
            created: [],
            rejected: [{ email: "ivanov@acme.ru", reason: "already-a-member" }],
        });

        expect(summary.isPartial).toBe(false);
        expect(describeInviteOutcome(summary)).toBe(
            "Ни одно приглашение не отправлено · отклонено адресов: 1"
        );
    });
});

describe("date and name formatting", () => {
    it("writes an invite expiry without the year — an invite lives days", () => {
        expect(formatShortRussianDate("2026-08-25T12:00:00Z")).toBe("25 авг.");
        expect(formatShortRussianDate(null)).toBe("—");
    });

    it("writes a joining date with the year and without the «г.» nobody says out loud", () => {
        expect(formatLongRussianDate("2026-03-12T12:00:00Z")).toBe("12 марта 2026");
        expect(formatLongRussianDate(null)).toBe("—");
    });

    it("echoes an unparseable date instead of printing «Invalid Date»", () => {
        expect(formatShortRussianDate("не дата")).toBe("не дата");
        expect(formatLongRussianDate("не дата")).toBe("не дата");
    });

    it("falls back to the email when a member has never set a display name", () => {
        expect(describeMemberName("Иванов А.", "ivanov@acme.ru")).toBe("Иванов А.");
        expect(describeMemberName("   ", "ivanov@acme.ru")).toBe("ivanov@acme.ru");
        expect(describeMemberName("", "")).toBe("Без имени");
    });

    it("builds initials from two words, or from the first letters of a single token", () => {
        expect(buildMemberInitials("Иванов Алексей", "ivanov@acme.ru")).toBe("ИА");
        expect(buildMemberInitials("", "ivanov@acme.ru")).toBe("IV");
    });
});
