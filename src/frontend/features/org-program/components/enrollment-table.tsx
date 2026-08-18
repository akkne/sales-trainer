"use client";

import { Button } from "@/shared/components/button";
import { Chip } from "@/shared/components/chip";
import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import {
    BEHIND_CHIP_LABEL,
    CURRENT_CHIP_LABEL,
    NO_ENROLLMENTS_DESCRIPTION,
    NO_ENROLLMENTS_TITLE,
    SWITCHED_HIMSELF_LABEL,
    WHAT_CHANGES_FOR_PERSON_LABEL,
} from "../constants/program-dictionary";
import {
    describeUnknownPerson,
    formatProgramDate,
    formatVersionLabel,
} from "../lib/format-program-text";
import { isEnrollmentBehind } from "../lib/program-versions";
import type { ProgramEnrollment, ProgramVersionSummary } from "../types/program";

interface EnrollmentTableProps {
    enrollments: ProgramEnrollment[];
    currentPublishedVersion: ProgramVersionSummary | null;
    memberNamesByUserId: Map<string, string>;
    isLoading: boolean;
    onShowPendingDiff: (enrollment: ProgramEnrollment) => void;
}

/**
 * One row per pin. The version column is the point of the table, and «Отстаёт» is stated rather than
 * implied: a reader scanning version labels alone will read `v2` next to `v3` as a typo.
 *
 * There is no control here that moves a pin, and there is no row selection that could grow into one.
 * The only action a row offers is reading what a move *would* change — the diff the learner will be
 * shown when they decide for themselves (docs/TENANCY/ADMIN_UI_DESIGN.md §7).
 */
export function EnrollmentTable({
    enrollments,
    currentPublishedVersion,
    memberNamesByUserId,
    isLoading,
    onShowPendingDiff,
}: EnrollmentTableProps) {
    const columns: Column<ProgramEnrollment>[] = [
        {
            key: "person",
            header: "Человек",
            render: (enrollment) => (
                <span className="font-medium text-ink">
                    {memberNamesByUserId.get(enrollment.userId) ??
                        describeUnknownPerson(enrollment.userId)}
                </span>
            ),
        },
        {
            key: "version",
            header: "Версия",
            render: (enrollment) => (
                <span className="flex items-center gap-2">
                    <span className="tnum text-ink" style={{ fontFamily: "var(--font-mono)" }}>
                        {formatVersionLabel(enrollment.programVersionNumber)}
                    </span>
                    {isEnrollmentBehind(enrollment, currentPublishedVersion) ? (
                        <Chip size="sm" tone="warn">
                            {BEHIND_CHIP_LABEL}
                        </Chip>
                    ) : (
                        currentPublishedVersion !== null && (
                            <Chip size="sm" tone="good">
                                {CURRENT_CHIP_LABEL}
                            </Chip>
                        )
                    )}
                </span>
            ),
        },
        {
            key: "since",
            header: "С какого дня",
            render: (enrollment) =>
                enrollment.switchedAt ? (
                    <span className="text-ink-2">
                        {SWITCHED_HIMSELF_LABEL} {formatProgramDate(enrollment.switchedAt)}
                    </span>
                ) : (
                    <span className="text-ink-2">
                        зачислен {formatProgramDate(enrollment.enrolledAt)}
                    </span>
                ),
        },
        {
            key: "pending",
            header: "",
            align: "right",
            render: (enrollment) =>
                isEnrollmentBehind(enrollment, currentPublishedVersion) ? (
                    <Button size="sm" variant="ghost" onClick={() => onShowPendingDiff(enrollment)}>
                        {WHAT_CHANGES_FOR_PERSON_LABEL}
                    </Button>
                ) : null,
        },
    ];

    return (
        <DataTable
            columns={columns}
            rows={enrollments}
            rowKey={(enrollment) => enrollment.userId}
            isLoading={isLoading}
            empty={
                <EmptyState
                    icon="users"
                    title={NO_ENROLLMENTS_TITLE}
                    description={NO_ENROLLMENTS_DESCRIPTION}
                />
            }
        />
    );
}
