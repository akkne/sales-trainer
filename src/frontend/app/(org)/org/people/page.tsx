"use client";

import { useState } from "react";
import { Chip } from "@/shared/components/common";
import { ConfirmDialog } from "@/shared/components/confirm-dialog";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { canManageOrganizationPeople, useAuthStore } from "@/shared/stores/auth-store";
import { InviteComposer } from "@/features/org-people/components/invite-composer";
import { InviteOutcomeList } from "@/features/org-people/components/invite-outcome-list";
import { PendingInvitesTable } from "@/features/org-people/components/pending-invites-table";
import { ReadOnlyNotice } from "@/features/org-people/components/read-only-notice";
import { RosterTable } from "@/features/org-people/components/roster-table";
import { DEFAULT_INVITE_ROLE } from "@/features/org-people/constants/people-dictionary";
import {
    describePeopleWriteFailure,
    useCreateInvites,
    useDeactivateMembership,
    useOrganizationInvites,
    useOrganizationMembers,
    useRevokeInvite,
} from "@/features/org-people/hooks/use-organization-people";
import type {
    CreateInvitesResponse,
    InviteStatusFilter,
    MembershipStatusFilter,
    OrganizationInvite,
    OrganizationMember,
} from "@/features/org-people/types/organization-people";
import { describeMemberName } from "@/features/org-people/utils/format-people";
import {
    buildInviteOutcomeLines,
    summarizeInviteOutcome,
    type InviteOutcomeLine,
    type InviteOutcomeSummary,
} from "@/features/org-people/utils/invite-outcome";

const PAGE_SUBTITLE =
    "Кто работает в компании, кого вы позвали и кто больше не с вами. Учётная запись появляется здесь только по приглашению — другой двери в систему нет.";

const ROLE_CHANGE_NOTE =
    "Роль участника не меняется: маршрута, который бы её менял, в API нет. Другая роль означает новое приглашение на тот же адрес.";

const DEACTIVATION_NOTE =
    "«Отключить» — это отзыв доступа, а не удаление человека. Прогресс, разговоры и строки в заданиях остаются: это история компании.";

interface LastInviteOutcome {
    lines: InviteOutcomeLine[];
    summary: InviteOutcomeSummary;
    role: string;
}

/**
 * O16 · «Люди» (docs/TENANCY/ADMIN_UI_DESIGN.md).
 *
 * Two reads and three writes, and the gap between them is the screen's whole shape: reading the
 * roster and the invite queue is `RequireOrgAdmin`, because a `TenancyAdmin` hands out assignments
 * to these people, while inviting, revoking and offboarding are `RequireOrgSuperAdmin`. An
 * administrator who is not a superadmin therefore gets the same two lists with no buttons at all —
 * and a sentence saying why, rather than a row of dead controls that read as breakage.
 */
export default function OrganizationPeoplePage() {
    const authenticatedUser = useAuthStore((state) => state.authenticatedUser);
    const canManagePeople = canManageOrganizationPeople(authenticatedUser?.orgRole);

    const [memberStatusFilter, setMemberStatusFilter] = useState<MembershipStatusFilter>("active");
    const [inviteStatusFilter, setInviteStatusFilter] = useState<InviteStatusFilter>("pending");

    const [rawEmails, setRawEmails] = useState("");
    const [inviteRole, setInviteRole] = useState<string>(DEFAULT_INVITE_ROLE);
    const [lastInviteOutcome, setLastInviteOutcome] = useState<LastInviteOutcome | null>(null);
    const [inviteErrorMessage, setInviteErrorMessage] = useState<string | null>(null);
    const [writeErrorMessage, setWriteErrorMessage] = useState<string | null>(null);
    const [memberPendingDeactivation, setMemberPendingDeactivation] =
        useState<OrganizationMember | null>(null);

    const membersQuery = useOrganizationMembers(memberStatusFilter);
    const invitesQuery = useOrganizationInvites(inviteStatusFilter);

    const createInvites = useCreateInvites();
    const revokeInvite = useRevokeInvite();
    const deactivateMembership = useDeactivateMembership();

    const submitInvites = (emails: string[], role: string) => {
        setInviteErrorMessage(null);
        createInvites.mutate(
            { emails, role },
            {
                onSuccess: (response: CreateInvitesResponse) => {
                    setLastInviteOutcome({
                        lines: buildInviteOutcomeLines(response, emails),
                        summary: summarizeInviteOutcome(response),
                        role,
                    });
                    setRawEmails("");
                },
                onError: (error: unknown) => {
                    setInviteErrorMessage(describePeopleWriteFailure(error));
                },
            }
        );
    };

    const submitRevoke = (invite: OrganizationInvite) => {
        setWriteErrorMessage(null);
        revokeInvite.mutate(invite.id, {
            onError: (error: unknown) => setWriteErrorMessage(describePeopleWriteFailure(error)),
        });
    };

    const confirmDeactivation = () => {
        if (!memberPendingDeactivation) return;
        setWriteErrorMessage(null);
        deactivateMembership.mutate(memberPendingDeactivation.userId, {
            onSuccess: () => setMemberPendingDeactivation(null),
            onError: (error: unknown) => {
                setWriteErrorMessage(describePeopleWriteFailure(error));
                setMemberPendingDeactivation(null);
            },
        });
    };

    const pendingDeactivationName = memberPendingDeactivation
        ? describeMemberName(
              memberPendingDeactivation.displayName,
              memberPendingDeactivation.email
          )
        : "";

    return (
        <>
            <PageHeader title="Люди" subtitle={PAGE_SUBTITLE} />

            {!canManagePeople && <ReadOnlyNotice />}

            {writeErrorMessage && (
                <p className="mb-6 text-sm" style={{ color: "var(--bad)" }} role="alert">
                    {writeErrorMessage}
                </p>
            )}

            {canManagePeople && (
                <InviteComposer
                    rawEmails={rawEmails}
                    onRawEmailsChange={setRawEmails}
                    role={inviteRole}
                    onRoleChange={setInviteRole}
                    onSubmit={submitInvites}
                    isPending={createInvites.isPending}
                    errorMessage={inviteErrorMessage}
                />
            )}

            {lastInviteOutcome && (
                <InviteOutcomeList
                    lines={lastInviteOutcome.lines}
                    summary={lastInviteOutcome.summary}
                    role={lastInviteOutcome.role}
                />
            )}

            <section className="mb-10">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                    <h2 className="text-xs font-medium uppercase tracking-wide text-ink-3">
                        Приглашения
                    </h2>
                    <div className="flex gap-2">
                        <Chip
                            selected={inviteStatusFilter === "pending"}
                            onClick={() => setInviteStatusFilter("pending")}
                        >
                            Ждут ответа
                        </Chip>
                        <Chip
                            selected={inviteStatusFilter === "all"}
                            onClick={() => setInviteStatusFilter("all")}
                        >
                            Все
                        </Chip>
                    </div>
                </div>

                {invitesQuery.isError ? (
                    <ErrorState
                        title="Не удалось загрузить приглашения"
                        message="Состав команды ниже это не затрагивает — списки читаются по отдельности."
                        onRetry={() => invitesQuery.refetch()}
                        compact
                    />
                ) : (
                    <PendingInvitesTable
                        invites={invitesQuery.data ?? []}
                        statusFilter={inviteStatusFilter}
                        isLoading={invitesQuery.isLoading}
                        canManagePeople={canManagePeople}
                        revokingInviteId={
                            revokeInvite.isPending ? (revokeInvite.variables ?? null) : null
                        }
                        onRevoke={submitRevoke}
                    />
                )}
            </section>

            <section>
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                    <h2 className="text-xs font-medium uppercase tracking-wide text-ink-3">
                        Состав команды
                    </h2>
                    <div className="flex gap-2">
                        <Chip
                            selected={memberStatusFilter === "active"}
                            onClick={() => setMemberStatusFilter("active")}
                        >
                            Работают
                        </Chip>
                        <Chip
                            selected={memberStatusFilter === "all"}
                            onClick={() => setMemberStatusFilter("all")}
                        >
                            С отключёнными
                        </Chip>
                    </div>
                </div>

                {membersQuery.isError ? (
                    <ErrorState
                        title="Не удалось загрузить состав команды"
                        message="Проверьте подключение и попробуйте снова."
                        onRetry={() => membersQuery.refetch()}
                        compact
                    />
                ) : (
                    <RosterTable
                        members={membersQuery.data ?? []}
                        statusFilter={memberStatusFilter}
                        isLoading={membersQuery.isLoading}
                        canManagePeople={canManagePeople}
                        currentUserId={authenticatedUser?.id ?? null}
                        deactivatingUserId={
                            deactivateMembership.isPending
                                ? (deactivateMembership.variables ?? null)
                                : null
                        }
                        onDeactivate={setMemberPendingDeactivation}
                    />
                )}

                <p className="mt-4 text-sm text-ink-3">{DEACTIVATION_NOTE}</p>
                <p className="mt-2 text-sm text-ink-3">{ROLE_CHANGE_NOTE}</p>
            </section>

            <ConfirmDialog
                open={memberPendingDeactivation !== null}
                title="Отключить доступ?"
                body={
                    <p className="text-sm text-ink-2">
                        {pendingDeactivationName} потеряет доступ к тренажёру. Прогресс, разговоры и
                        строки в заданиях останутся — это история компании. Вернуть человека можно
                        новым приглашением на тот же адрес.
                    </p>
                }
                confirmLabel="Отключить"
                tone="danger"
                onConfirm={confirmDeactivation}
                onCancel={() => setMemberPendingDeactivation(null)}
                isPending={deactivateMembership.isPending}
            />
        </>
    );
}
