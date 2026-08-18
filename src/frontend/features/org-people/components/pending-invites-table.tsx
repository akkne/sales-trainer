"use client";

import { Badge } from "@/shared/components/common";
import { Button } from "@/shared/components/button";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import {
    INVITE_STATUS_PENDING,
    describeInviteStatus,
    describeOrganizationRole,
} from "@/features/org-people/constants/people-dictionary";
import type {
    InviteStatusFilter,
    OrganizationInvite,
} from "@/features/org-people/types/organization-people";
import { formatShortRussianDate } from "@/features/org-people/utils/format-people";

const STATUS_BADGE_VARIANTS: Record<string, "neutral" | "success" | "error"> = {
    pending: "neutral",
    accepted: "success",
    revoked: "error",
    expired: "error",
};

interface PendingInvitesTableProps {
    invites: OrganizationInvite[];
    statusFilter: InviteStatusFilter;
    isLoading: boolean;
    canManagePeople: boolean;
    revokingInviteId: string | null;
    onRevoke: (invite: OrganizationInvite) => void;
}

function buildEmptyState(statusFilter: InviteStatusFilter) {
    if (statusFilter === "pending") {
        return (
            <EmptyState
                icon="send"
                title="Непринятых приглашений нет"
                description="Здесь ждут те, кому вы уже написали, но кто ещё не завёл учётную запись. Принятое приглашение — это уже участник, ищите его в составе команды."
                compact
            />
        );
    }

    return (
        <EmptyState
            icon="send"
            title="Приглашений ещё не было"
            description="В закрытой системе человек попадает в компанию только по приглашению — других дверей нет."
            compact
        />
    );
}

/// The invite queue. Statuses come from the server already decided; the browser neither recomputes
/// «истекло» against its own clock nor sorts an accepted invite into the roster on its own.
export function PendingInvitesTable({
    invites,
    statusFilter,
    isLoading,
    canManagePeople,
    revokingInviteId,
    onRevoke,
}: PendingInvitesTableProps) {
    const columns: Column<OrganizationInvite>[] = [
        {
            key: "email",
            header: "Адрес",
            render: (invite) => <span className="text-ink">{invite.email}</span>,
        },
        {
            key: "role",
            header: "Роль",
            render: (invite) => describeOrganizationRole(invite.role),
        },
        {
            key: "status",
            header: "Статус",
            render: (invite) => (
                <Badge variant={STATUS_BADGE_VARIANTS[invite.status] ?? "neutral"} size="sm">
                    {describeInviteStatus(invite.status)}
                </Badge>
            ),
        },
        {
            key: "expiresAt",
            header: "Действует до",
            render: (invite) => (
                <span style={{ fontFamily: "var(--font-mono)" }}>
                    {formatShortRussianDate(invite.expiresAt)}
                </span>
            ),
        },
    ];

    if (canManagePeople) {
        columns.push({
            key: "actions",
            header: "",
            align: "right",
            render: (invite) =>
                invite.status === INVITE_STATUS_PENDING ? (
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onRevoke(invite)}
                        loading={revokingInviteId === invite.id}
                        disabled={revokingInviteId !== null}
                    >
                        Отозвать
                    </Button>
                ) : null,
        });
    }

    return (
        <DataTable
            columns={columns}
            rows={invites}
            rowKey={(invite) => invite.id}
            empty={buildEmptyState(statusFilter)}
            isLoading={isLoading}
        />
    );
}
