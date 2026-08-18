"use client";

import { Avatar, Badge } from "@/shared/components/common";
import { Button } from "@/shared/components/button";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import {
    describeMembershipStatus,
    describeOrganizationRole,
} from "@/features/org-people/constants/people-dictionary";
import type {
    MembershipStatusFilter,
    OrganizationMember,
} from "@/features/org-people/types/organization-people";
import {
    buildMemberInitials,
    describeMemberName,
    formatLongRussianDate,
} from "@/features/org-people/utils/format-people";

const ACTIVE_MEMBERSHIP_STATUS = "Active";

export const SELF_ROW_LABEL = "это вы";

interface RosterTableProps {
    members: OrganizationMember[];
    statusFilter: MembershipStatusFilter;
    isLoading: boolean;
    canManagePeople: boolean;
    currentUserId: string | null;
    deactivatingUserId: string | null;
    onDeactivate: (member: OrganizationMember) => void;
}

function buildEmptyState(statusFilter: MembershipStatusFilter) {
    if (statusFilter === "active") {
        return (
            <EmptyState
                icon="users"
                title="В компании пока никто не работает"
                description="Новая организация — это один человек. Пригласите менеджеров: они появятся здесь, как только примут приглашение."
                compact
            />
        );
    }

    return (
        <EmptyState
            icon="users"
            title="Ни одной учётной записи"
            description="Ни действующих участников, ни отключённых — в этой организации ещё никто не принимал приглашение."
            compact
        />
    );
}

/// Who works here, and who used to. A deactivated row stays in the list forever on purpose: the
/// person's attempts, conversations and assignment rows are the organization's history, and a
/// roster that forgot them would make that history unattributable.
export function RosterTable({
    members,
    statusFilter,
    isLoading,
    canManagePeople,
    currentUserId,
    deactivatingUserId,
    onDeactivate,
}: RosterTableProps) {
    const columns: Column<OrganizationMember>[] = [
        {
            key: "person",
            header: "Человек",
            render: (member) => (
                <div className="flex items-center gap-3">
                    <Avatar
                        initials={buildMemberInitials(member.displayName, member.email)}
                        size="sm"
                    />
                    <div className="min-w-0">
                        <div className="flex items-center gap-2">
                            <span className="text-ink">
                                {describeMemberName(member.displayName, member.email)}
                            </span>
                            {member.userId === currentUserId && (
                                <Badge variant="neutral" size="sm">
                                    {SELF_ROW_LABEL}
                                </Badge>
                            )}
                        </div>
                        <div className="text-xs text-ink-3">{member.email}</div>
                    </div>
                </div>
            ),
        },
        {
            key: "role",
            header: "Роль",
            render: (member) => describeOrganizationRole(member.role),
        },
        {
            key: "joinedAt",
            header: "В компании с",
            render: (member) => (
                <span style={{ fontFamily: "var(--font-mono)" }}>
                    {formatLongRussianDate(member.joinedAt)}
                </span>
            ),
        },
    ];

    if (statusFilter === "all") {
        columns.push({
            key: "status",
            header: "Статус",
            render: (member) =>
                member.status === ACTIVE_MEMBERSHIP_STATUS ? (
                    <Badge variant="neutral" size="sm">
                        {describeMembershipStatus(member.status)}
                    </Badge>
                ) : (
                    <div className="flex flex-col gap-1">
                        <Badge variant="error" size="sm">
                            {describeMembershipStatus(member.status)}
                        </Badge>
                        <span className="text-xs text-ink-3">
                            с {formatLongRussianDate(member.deactivatedAt)}
                        </span>
                    </div>
                ),
        });
    }

    if (canManagePeople) {
        columns.push({
            key: "actions",
            header: "",
            align: "right",
            render: (member) =>
                member.status === ACTIVE_MEMBERSHIP_STATUS && member.userId !== currentUserId ? (
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onDeactivate(member)}
                        loading={deactivatingUserId === member.userId}
                        disabled={deactivatingUserId !== null}
                    >
                        Отключить
                    </Button>
                ) : null,
        });
    }

    return (
        <DataTable
            columns={columns}
            rows={members}
            rowKey={(member) => member.userId}
            empty={buildEmptyState(statusFilter)}
            isLoading={isLoading}
        />
    );
}
