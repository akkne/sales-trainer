"use client";

import type { ReactNode } from "react";
import type { AssignmentFunnel as AssignmentFunnelData } from "@/features/org-assignments/types/assignment";
import { buildDashboardFunnelStages } from "@/features/org-assignments/utils/funnel";
import { pluralizePeople } from "@/features/org-assignments/utils/audience-rule";

interface AssignmentFunnelProps {
    funnel: AssignmentFunnelData;
    /** False when identity-service could not be asked who still works here. */
    isRosterKnown: boolean;
    action?: ReactNode;
}

/**
 * The five-stage funnel of O4. «Ниже порога» is a column, not a slice of «выполнили»: the people who
 * did the work and stayed under the bar are the reason the screen exists.
 *
 * An all-zero funnel on an issued assignment is the ordinary first day and says so, rather than
 * reading as a failure to load.
 */
export function AssignmentFunnel({ funnel, isRosterKnown, action }: AssignmentFunnelProps) {
    const stages = buildDashboardFunnelStages(funnel);
    const hasNobodyStarted = funnel.startedCount === 0 && funnel.assignedCount > 0;

    return (
        <div>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
                {stages.map((stage) => (
                    <div key={stage.key}>
                        <div className="text-xs text-ink-3">{stage.label}</div>
                        <div
                            className="tnum mt-1 text-2xl font-bold text-ink"
                            style={{ fontFamily: "var(--font-mono)" }}
                        >
                            {stage.count}
                        </div>
                        <div
                            className="mt-2 h-1.5 w-full overflow-hidden"
                            style={{ background: "var(--bg-2)", borderRadius: "999px" }}
                        >
                            <div
                                style={{
                                    width: `${stage.filledPercent}%`,
                                    height: "100%",
                                    background: stage.color,
                                }}
                            />
                        </div>
                    </div>
                ))}
            </div>

            <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                <p className="text-sm text-ink-3">
                    {!isRosterKnown
                        ? "Не удалось проверить, кто ещё работает в компании."
                        : describeRoster(funnel)}
                </p>
                {action}
            </div>

            {hasNobodyStarted && (
                <p className="mt-2 text-sm text-ink-3">
                    Выдано {funnel.assignedCount} · пока никто не начал.
                </p>
            )}
        </div>
    );
}

function describeRoster(funnel: AssignmentFunnelData): string {
    const leftCount = funnel.leftOrganizationCount ?? 0;
    if (leftCount === 0) return "Все, кому выдано задание, работают в компании.";

    const activeCount = funnel.assignedActiveCount ?? funnel.assignedCount - leftCount;

    return `Из ${funnel.assignedCount} ${leftCount} ${pluralizePeople(leftCount)} уже не ${leftCount === 1 ? "работает" : "работают"} в компании. Активных: ${activeCount}.`;
}
