"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import {
    assignmentStatusTone,
    describeAssignmentSourceType,
    describeAssignmentStatus,
} from "@/features/org-assignments/constants/assignment-dictionary";
import type { AssignmentSummary } from "@/features/org-assignments/types/assignment";
import {
    useDeleteAssignment,
    useOrganizationAssignments,
} from "@/features/org-assignments/hooks/use-org-assignments";
import { AssignmentFunnelBar } from "@/features/org-assignments/components/assignment-funnel-bar";
import {
    describeAudienceKind,
    pluralizeContentItems,
} from "@/features/org-assignments/utils/audience-rule";
import { describeDeadline } from "@/features/org-assignments/utils/funnel";

const STATUS_FILTERS: { key: string; label: string }[] = [
    { key: "all", label: "Все" },
    { key: "active", label: "Выдано" },
    { key: "draft", label: "Черновики" },
    { key: "closed", label: "Закрыто" },
];

/**
 * O2 — the list of what the РОП handed the team, with the funnel inside the row.
 *
 * The whole array is read once and filtered by chips on the client: `GET /admin/assignments` takes a
 * status but does not paginate and has no counts endpoint, so four requests would buy nothing except
 * four chances to disagree with each other about how many drafts there are.
 */
export default function OrganizationAssignmentsPage() {
    const router = useRouter();
    const assignmentsQuery = useOrganizationAssignments();
    const deleteAssignmentMutation = useDeleteAssignment();
    const [activeStatusFilter, setActiveStatusFilter] = useState("all");
    const [assignmentPendingDeletion, setAssignmentPendingDeletion] = useState<string | null>(null);

    const assignments = useMemo(() => assignmentsQuery.data ?? [], [assignmentsQuery.data]);

    const titleByAssignmentId = useMemo(() => {
        const titles = new Map<string, string>();
        for (const assignment of assignments) titles.set(assignment.id, assignment.title);
        return titles;
    }, [assignments]);

    const countByStatus = useMemo(() => {
        const counts: Record<string, number> = { all: assignments.length };
        for (const assignment of assignments) {
            counts[assignment.status] = (counts[assignment.status] ?? 0) + 1;
        }
        return counts;
    }, [assignments]);

    const visibleAssignments =
        activeStatusFilter === "all"
            ? assignments
            : assignments.filter((assignment) => assignment.status === activeStatusFilter);

    const columns: Column<AssignmentSummary>[] = [
        {
            key: "title",
            header: "Название",
            render: (assignment) => (
                <div className="min-w-[220px]">
                    <div className="font-medium text-ink">
                        {assignment.title}
                        {assignment.repeatWaveIndex !== null && (
                            <span className="text-ink-3"> · волна {assignment.repeatWaveIndex + 1}</span>
                        )}
                    </div>
                    <div className="text-xs text-ink-3">
                        {[
                            describeAudienceKind(
                                assignment.audienceKind,
                                assignment.assignedCount || null
                            ),
                            `${assignment.contentItemCount} ${pluralizeContentItems(assignment.contentItemCount)}`,
                            assignment.hasRepeatSchedule ? "с повторами" : null,
                        ]
                            .filter(Boolean)
                            .join(" · ")}
                    </div>
                    {assignment.repeatOfAssignmentId !== null && (
                        <div className="text-xs text-ink-4">
                            ↳ повтор
                            {titleByAssignmentId.has(assignment.repeatOfAssignmentId)
                                ? ` задания «${titleByAssignmentId.get(assignment.repeatOfAssignmentId)}»`
                                : ""}
                        </div>
                    )}
                </div>
            ),
        },
        {
            key: "sourceType",
            header: "Источник",
            render: (assignment) => (
                <Chip tone="neutral" size="sm">
                    {describeAssignmentSourceType(assignment.sourceType)}
                </Chip>
            ),
        },
        {
            key: "deadline",
            header: "Срок",
            render: (assignment) => {
                const deadline = describeDeadline(assignment.deadline);
                const isAmber = deadline.isOverdue && assignment.status === "active";

                return (
                    <span
                        className="text-sm"
                        style={isAmber ? { color: "var(--amber)" } : undefined}
                    >
                        {deadline.text}
                    </span>
                );
            },
        },
        {
            key: "funnel",
            header: "Воронка",
            render: (assignment) => <AssignmentFunnelBar summary={assignment} />,
        },
        {
            key: "status",
            header: "Статус",
            align: "right",
            render: (assignment) => (
                <div className="flex items-center justify-end gap-2">
                    <Chip tone={assignmentStatusTone(assignment.status)} size="sm">
                        {describeAssignmentStatus(assignment.status)}
                    </Chip>
                    {assignment.status === "draft" && (
                        <Button
                            size="sm"
                            variant={
                                assignmentPendingDeletion === assignment.id ? "destructive" : "ghost"
                            }
                            loading={
                                deleteAssignmentMutation.isPending &&
                                assignmentPendingDeletion === assignment.id
                            }
                            onClick={(clickEvent) => {
                                clickEvent.stopPropagation();
                                if (assignmentPendingDeletion === assignment.id) {
                                    deleteAssignmentMutation.mutate(assignment.id, {
                                        onSettled: () => setAssignmentPendingDeletion(null),
                                    });
                                    return;
                                }
                                setAssignmentPendingDeletion(assignment.id);
                            }}
                        >
                            {assignmentPendingDeletion === assignment.id
                                ? "Точно удалить?"
                                : "Удалить"}
                        </Button>
                    )}
                </div>
            ),
        },
    ];

    return (
        <>
            <PageHeader
                title="Задания"
                subtitle="Короткая практика с дедлайном и порогом, выданная команде."
                action={
                    <Link href="/org/assignments/new">
                        <Button variant="primary" iconLeft="plus">
                            Новое задание
                        </Button>
                    </Link>
                }
            />

            <div className="mb-4 flex flex-wrap gap-2">
                {STATUS_FILTERS.map((filter) => (
                    <Chip
                        key={filter.key}
                        tone="neutral"
                        active={activeStatusFilter === filter.key}
                        onClick={() => setActiveStatusFilter(filter.key)}
                    >
                        {filter.label} {countByStatus[filter.key] ?? 0}
                    </Chip>
                ))}
            </div>

            {assignmentsQuery.isError ? (
                <ErrorState
                    title="Не удалось загрузить задания"
                    message="Попробуйте ещё раз — данные не потеряны."
                    onRetry={() => void assignmentsQuery.refetch()}
                />
            ) : (
                <DataTable
                    columns={columns}
                    rows={visibleAssignments}
                    rowKey={(assignment) => assignment.id}
                    onRowClick={(assignment) => router.push(`/org/assignments/${assignment.id}`)}
                    isLoading={assignmentsQuery.isLoading}
                    empty={
                        assignments.length === 0 ? (
                            <EmptyState
                                icon="grid"
                                title="Заданий пока нет"
                                description="Задание — это короткая практика с дедлайном и порогом, которую вы выдаёте команде после внутреннего тренинга."
                                action={
                                    <Link href="/org/assignments/new">
                                        <Button variant="primary">Создать первое задание</Button>
                                    </Link>
                                }
                            />
                        ) : (
                            <EmptyState
                                icon="grid"
                                compact
                                title="В этом статусе ничего нет"
                                action={
                                    <Button
                                        variant="secondary"
                                        onClick={() => setActiveStatusFilter("all")}
                                    >
                                        Показать все
                                    </Button>
                                }
                            />
                        )
                    }
                />
            )}

            {deleteAssignmentMutation.isError && (
                <p className="mt-3 text-sm" style={{ color: "var(--heart)" }} role="alert">
                    Черновик не удалился. Выданное задание удалить нельзя — его можно только закрыть.
                </p>
            )}
        </>
    );
}
