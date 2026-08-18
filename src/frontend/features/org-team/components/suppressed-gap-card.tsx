"use client";

import Link from "next/link";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import {
    GAP_SUPPRESSION_REASONS,
    describeGapSuppressionReason,
} from "@/features/org-team/constants/gap-suppression";
import { formatWindowStartDate } from "@/features/org-team/utils/team-summary";
import type { SuppressedTeamSkillGap } from "@/features/org-team/hooks/use-team-skill-gaps";

interface SuppressedGapCardProps {
    suppressedGap: SuppressedTeamSkillGap;
    onRestore: (stageKey: string) => void;
    isRestoring: boolean;
    restoreErrorMessage: string | null;
}

function describeReturnDate(suppressedUntil: string | null): string | null {
    if (!suppressedUntil) return null;
    const formattedDate = formatWindowStartDate(suppressedUntil);
    return formattedDate ? `до ${formattedDate}` : null;
}

/// A stage that is failing and is deliberately not being offered, with the reason and the date the
/// reason runs out.
///
/// It is rendered even when there is nothing to press. «Почему мне ничего не предлагают» is the
/// question that gets a feature switched off, and a panel that answers it by showing nothing cannot
/// be told apart from a panel that is broken.
export function SuppressedGapCard({
    suppressedGap,
    onRestore,
    isRestoring,
    restoreErrorMessage,
}: SuppressedGapCardProps) {
    const returnDate = describeReturnDate(suppressedGap.suppressedUntil);
    const isDismissedByAdministrator =
        suppressedGap.reason === GAP_SUPPRESSION_REASONS.dismissed;
    const runLink =
        suppressedGap.reason === GAP_SUPPRESSION_REASONS.runInProgress &&
        suppressedGap.contentGenerationJobId
            ? `/org/content/generation/${suppressedGap.contentGenerationJobId}`
            : null;

    return (
        <Card padding={16} style={{ background: "var(--bg-2)" }}>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                    <p className="text-sm font-medium text-ink">
                        {suppressedGap.stageLabel}
                        <span className="mono text-ink-3 font-normal">
                            {" · "}
                            {suppressedGap.accuracyPercent}%, {suppressedGap.attemptCount}
                        </span>
                        <span className="text-ink-3 font-normal"> попыток</span>
                    </p>
                    <p className="mt-1 text-xs text-ink-3">
                        {describeGapSuppressionReason(suppressedGap.reason)}
                        {returnDate ? ` ${returnDate}` : ""}
                        {runLink && (
                            <>
                                {" · "}
                                <Link href={runLink} className="text-primary-ink underline">
                                    открыть прогон
                                </Link>
                            </>
                        )}
                    </p>
                </div>

                {isDismissedByAdministrator && (
                    <Button
                        variant="ghost"
                        size="sm"
                        loading={isRestoring}
                        onClick={() => onRestore(suppressedGap.stageKey)}
                    >
                        Вернуть предложение
                    </Button>
                )}
            </div>

            {restoreErrorMessage && (
                <p className="mt-2 text-xs" style={{ color: "var(--heart)" }} role="alert">
                    {restoreErrorMessage}
                </p>
            )}
        </Card>
    );
}
