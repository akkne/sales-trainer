import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { InviteOutcomeList } from "@/features/org-people/components/invite-outcome-list";
import { PendingInvitesTable } from "@/features/org-people/components/pending-invites-table";
import {
    PEOPLE_READ_ONLY_MESSAGE,
    ReadOnlyNotice,
} from "@/features/org-people/components/read-only-notice";
import { RosterTable } from "@/features/org-people/components/roster-table";
import type {
    CreateInvitesResponse,
    OrganizationInvite,
    OrganizationMember,
} from "@/features/org-people/types/organization-people";
import {
    buildInviteOutcomeLines,
    summarizeInviteOutcome,
} from "@/features/org-people/utils/invite-outcome";

const FORBIDDEN_GAMIFICATION_PATTERNS = [/\bXP\b/i, /стрик/i, /лиг[аиуе]/i, /очк[иов]/i];

function buildInvite(overrides: Partial<OrganizationInvite>): OrganizationInvite {
    return {
        id: "invite-ivanov",
        email: "ivanov@acme.ru",
        role: "Manager",
        status: "pending",
        invitedBy: "superadmin-id",
        createdAt: "2026-08-18T12:00:00Z",
        expiresAt: "2026-08-25T12:00:00Z",
        ...overrides,
    };
}

function buildMember(overrides: Partial<OrganizationMember>): OrganizationMember {
    return {
        userId: "user-ivanov",
        email: "ivanov@acme.ru",
        displayName: "Иванов Алексей",
        role: "Manager",
        status: "Active",
        joinedAt: "2026-03-12T12:00:00Z",
        deactivatedAt: null,
        ...overrides,
    };
}

describe("InviteOutcomeList — a partial answer rendered as one list", () => {
    const submittedEmails = ["ivanov@acme.ru", "petrov@acme.ru", "sokolova@acme.ru"];

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
        rejected: [{ email: "sokolova@acme.ru", reason: "already-a-member" }],
    };

    function renderOutcome(outcomeResponse: CreateInvitesResponse, emails: string[]) {
        return render(
            <InviteOutcomeList
                lines={buildInviteOutcomeLines(outcomeResponse, emails)}
                summary={summarizeInviteOutcome(outcomeResponse)}
                role="Manager"
            />
        );
    }

    it("shows both halves — two accepted addresses and the refused one with its reason", () => {
        renderOutcome(response, submittedEmails);

        expect(screen.getByText("ivanov@acme.ru")).toBeTruthy();
        expect(screen.getByText("petrov@acme.ru")).toBeTruthy();
        expect(screen.getByText("sokolova@acme.ru")).toBeTruthy();
        expect(screen.getByText("уже в компании")).toBeTruthy();
        expect(screen.getAllByText("действует до 25 авг.")).toHaveLength(2);
    });

    it("states both counts, so a 37-of-40 result reads as neither success nor failure", () => {
        renderOutcome(response, submittedEmails);

        expect(
            screen.getByText("Отправлено приглашений: 2 · отклонено адресов: 1")
        ).toBeTruthy();
    });

    it("never prints the raw invite token, even though the creation response carries one", () => {
        const responseWithToken = {
            created: [
                {
                    id: "invite-ivanov",
                    email: "ivanov@acme.ru",
                    role: "Manager",
                    expiresAt: "2026-08-25T12:00:00Z",
                    token: "raw-single-use-token-8f21c0",
                },
            ],
            rejected: [],
        } as unknown as CreateInvitesResponse;

        const { container } = renderOutcome(responseWithToken, ["ivanov@acme.ru"]);

        expect(container.textContent).not.toContain("raw-single-use-token-8f21c0");
        expect(container.querySelector("a")).toBeNull();
        expect(screen.getByText(/ушла на почту/)).toBeTruthy();
    });

    it("renders nothing at all when there is no answer to show", () => {
        const { container } = renderOutcome({ created: [], rejected: [] }, []);

        expect(container.textContent).toBe("");
    });

    it("mentions no XP, streaks or leagues", () => {
        const { container } = renderOutcome(response, submittedEmails);

        for (const pattern of FORBIDDEN_GAMIFICATION_PATTERNS) {
            expect(pattern.test(container.textContent ?? "")).toBe(false);
        }
    });
});

describe("PendingInvitesTable", () => {
    it("renders all four derived statuses in their own words", () => {
        render(
            <PendingInvitesTable
                invites={[
                    buildInvite({ id: "a", email: "a@acme.ru", status: "pending" }),
                    buildInvite({ id: "b", email: "b@acme.ru", status: "accepted" }),
                    buildInvite({ id: "c", email: "c@acme.ru", status: "revoked" }),
                    buildInvite({ id: "d", email: "d@acme.ru", status: "expired" }),
                ]}
                statusFilter="all"
                isLoading={false}
                canManagePeople
                revokingInviteId={null}
                onRevoke={vi.fn()}
            />
        );

        expect(screen.getByText("Ждёт ответа")).toBeTruthy();
        expect(screen.getByText("Принято")).toBeTruthy();
        expect(screen.getByText("Отозвано")).toBeTruthy();
        expect(screen.getByText("Истекло")).toBeTruthy();
    });

    it("offers «Отозвать» only on the invite that can still be revoked", () => {
        render(
            <PendingInvitesTable
                invites={[
                    buildInvite({ id: "a", email: "a@acme.ru", status: "pending" }),
                    buildInvite({ id: "b", email: "b@acme.ru", status: "accepted" }),
                    buildInvite({ id: "c", email: "c@acme.ru", status: "expired" }),
                ]}
                statusFilter="all"
                isLoading={false}
                canManagePeople
                revokingInviteId={null}
                onRevoke={vi.fn()}
            />
        );

        expect(screen.getAllByRole("button", { name: "Отозвать" })).toHaveLength(1);
    });

    it("shows a TenancyAdmin the same invites with no action column at all", () => {
        render(
            <PendingInvitesTable
                invites={[buildInvite({})]}
                statusFilter="pending"
                isLoading={false}
                canManagePeople={false}
                revokingInviteId={null}
                onRevoke={vi.fn()}
            />
        );

        expect(screen.getByText("ivanov@acme.ru")).toBeTruthy();
        expect(screen.queryByRole("button", { name: "Отозвать" })).toBeNull();
    });

    it("explains the section when the pending queue is empty rather than drawing an empty table", () => {
        const { container } = render(
            <PendingInvitesTable
                invites={[]}
                statusFilter="pending"
                isLoading={false}
                canManagePeople
                revokingInviteId={null}
                onRevoke={vi.fn()}
            />
        );

        expect(screen.getByText("Непринятых приглашений нет")).toBeTruthy();
        expect(container.querySelector("table")).toBeNull();
    });

    it("shows a skeleton while the read is in flight, never an empty state", () => {
        render(
            <PendingInvitesTable
                invites={[]}
                statusFilter="pending"
                isLoading
                canManagePeople
                revokingInviteId={null}
                onRevoke={vi.fn()}
            />
        );

        expect(screen.getByLabelText("Загрузка...")).toBeTruthy();
        expect(screen.queryByText("Непринятых приглашений нет")).toBeNull();
    });
});

describe("RosterTable", () => {
    it("offboarding reads as «Отключить», never as deleting a person", () => {
        const { container } = render(
            <RosterTable
                members={[buildMember({})]}
                statusFilter="active"
                isLoading={false}
                canManagePeople
                currentUserId="superadmin-id"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(screen.getByRole("button", { name: "Отключить" })).toBeTruthy();
        expect(container.textContent).not.toMatch(/удал/i);
    });

    it("hides every write control from a TenancyAdmin but keeps the roster", () => {
        render(
            <RosterTable
                members={[buildMember({})]}
                statusFilter="active"
                isLoading={false}
                canManagePeople={false}
                currentUserId="superadmin-id"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(screen.getByText("Иванов Алексей")).toBeTruthy();
        expect(screen.queryByRole("button", { name: "Отключить" })).toBeNull();
    });

    it("keeps a deactivated person on the list, dated, and with nothing left to press", () => {
        render(
            <RosterTable
                members={[
                    buildMember({
                        userId: "user-orlov",
                        displayName: "Орлов Пётр",
                        email: "orlov@acme.ru",
                        status: "Deactivated",
                        deactivatedAt: "2026-07-01T12:00:00Z",
                    }),
                ]}
                statusFilter="all"
                isLoading={false}
                canManagePeople
                currentUserId="superadmin-id"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(screen.getByText("Орлов Пётр")).toBeTruthy();
        expect(screen.getByText("Отключён")).toBeTruthy();
        expect(screen.getByText("с 1 июля 2026")).toBeTruthy();
        expect(screen.queryByRole("button", { name: "Отключить" })).toBeNull();
    });

    it("does not offer the superadmin a button that would lock them out of their own company", () => {
        render(
            <RosterTable
                members={[buildMember({ userId: "user-ivanov" })]}
                statusFilter="active"
                isLoading={false}
                canManagePeople
                currentUserId="user-ivanov"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(screen.getByText("это вы")).toBeTruthy();
        expect(screen.queryByRole("button", { name: "Отключить" })).toBeNull();
    });

    it("offers no control that would change a member's role — there is no such route", () => {
        const { container } = render(
            <RosterTable
                members={[buildMember({})]}
                statusFilter="all"
                isLoading={false}
                canManagePeople
                currentUserId="superadmin-id"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(container.querySelector("select")).toBeNull();
        expect(screen.getByText("Менеджер")).toBeTruthy();
        expect(screen.queryByRole("button", { name: /роль/i })).toBeNull();
    });

    it("greets a brand-new organization with an explanation, not with a zero", () => {
        render(
            <RosterTable
                members={[]}
                statusFilter="active"
                isLoading={false}
                canManagePeople
                currentUserId="superadmin-id"
                deactivatingUserId={null}
                onDeactivate={vi.fn()}
            />
        );

        expect(screen.getByText("В компании пока никто не работает")).toBeTruthy();
    });
});

describe("ReadOnlyNotice", () => {
    it("says who may invite and offboard, and that reading is not the restricted part", () => {
        const { container } = render(<ReadOnlyNotice />);

        expect(screen.getByText(PEOPLE_READ_ONLY_MESSAGE)).toBeTruthy();
        expect(container.querySelectorAll("button")).toHaveLength(0);
    });
});
