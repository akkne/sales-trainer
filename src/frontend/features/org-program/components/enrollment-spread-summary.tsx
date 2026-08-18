"use client";

import { Chip } from "@/shared/components/chip";
import { Icon } from "@/shared/components/icon";
import { ROSTER_UNAVAILABLE_NOTE } from "../constants/program-dictionary";
import {
    describePersonCount,
    formatProgramDate,
    formatVersionLabel,
} from "../lib/format-program-text";
import type { EnrollmentSpread } from "../lib/program-versions";
import type { ProgramVersionSummary } from "../types/program";

interface EnrollmentSpreadSummaryProps {
    spread: EnrollmentSpread;
    currentPublishedVersion: ProgramVersionSummary;
    /**
     * Three states, not a boolean. A roster still in flight and a roster that failed both leave
     * `notEnrolledCount` at zero, and «без зачисления никого нет» would then be a false statement
     * rather than a missing one — which is the specific way this screen would mislead.
     */
    rosterState: "loading" | "ready" | "unavailable";
}

/**
 * Who is on which version, in the three sentences a reader must not be able to misread.
 *
 * The third one is the one that is easy to leave out and expensive to leave out: somebody who was
 * never enrolled is **not** on the newest version, they are on the live skill tree and see every
 * edit the moment it lands. A screen that reports «7 из 9 на последней версии» and says nothing
 * about the other people in the organization has told the reader that the programme is in force
 * when it is not.
 */
export function EnrollmentSpreadSummary({
    spread,
    currentPublishedVersion,
    rosterState,
}: EnrollmentSpreadSummaryProps) {
    const publishedOn = formatProgramDate(currentPublishedVersion.publishedAt);

    return (
        <div className="flex flex-col gap-3">
            <p className="text-sm text-ink-2">
                Последняя опубликованная версия —{" "}
                <span className="font-medium text-ink tnum" style={{ fontFamily: "var(--font-mono)" }}>
                    {formatVersionLabel(currentPublishedVersion.versionNumber)}
                </span>
                {publishedOn ? `, опубликована ${publishedOn}` : ""}. На неё попадают только те, кого вы
                зачислите сейчас.
            </p>

            <div className="flex flex-wrap items-center gap-2">
                {spread.groups.map((group) => (
                    <Chip
                        key={group.programVersionId}
                        tone={group.isCurrentPublishedVersion ? "good" : "warn"}
                    >
                        {formatVersionLabel(group.programVersionNumber)} ·{" "}
                        {describePersonCount(group.enrollmentCount)}
                    </Chip>
                ))}
                {spread.enrolledCount === 0 && (
                    <Chip tone="neutral">Ни одного зачисления</Chip>
                )}
            </div>

            {spread.isSpreadAcrossVersions && (
                <div
                    role="status"
                    className="flex items-start gap-3 rounded-2xl p-3"
                    style={{ background: "var(--warn-soft)" }}
                >
                    <Icon
                        name="info"
                        size="md"
                        style={{ color: "oklch(0.45 0.10 80)", flexShrink: 0, marginTop: 2 }}
                    />
                    <p className="text-sm text-ink-2">
                        Команда учится по разным версиям:{" "}
                        {describePersonCount(spread.onCurrentVersionCount)} на{" "}
                        {formatVersionLabel(currentPublishedVersion.versionNumber)},{" "}
                        {describePersonCount(spread.behindCount)} на более ранних. Это нормальное
                        состояние, а не рассинхронизация: программу человека не двигает никто, кроме него
                        самого.
                    </p>
                </div>
            )}

            {rosterState === "ready" && spread.notEnrolledCount > 0 && (
                <p className="text-sm text-ink-2">
                    Без зачисления —{" "}
                    <span className="font-medium text-ink">
                        {describePersonCount(spread.notEnrolledCount)}
                    </span>
                    : они учатся по живому дереву навыков и видят каждое изменение сразу, а не по
                    зафиксированной версии.
                </p>
            )}

            {rosterState === "ready" && spread.notEnrolledCount === 0 && spread.enrolledCount > 0 && (
                <p className="text-sm text-ink-2">
                    Без зачисления никого нет: вся команда учится по зафиксированной версии.
                </p>
            )}

            {rosterState === "unavailable" && <p className="text-xs text-ink-3">{ROSTER_UNAVAILABLE_NOTE}</p>}
        </div>
    );
}
