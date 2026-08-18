"use client";

import { useMemo } from "react";
import { Chip } from "@/shared/components/chip";
import { EmptyState } from "@/shared/components/empty-state";
import {
    describeFindingCode,
    describeFindingSeverity,
    findingSeverityTone,
} from "@/features/org-content-adaptation/constants/adaptation-dictionary";
import type { ContentReviewFinding } from "@/features/org-content-adaptation/types/adaptation";

/**
 * What the review found about one exercise (O13, `quality_review`).
 *
 * <b>Seven codes, and the sentence is the server's.</b> `ContentReviewFindingCodes` is a closed
 * vocabulary: ai-service returns a code and at most the quoted fragment, learning-service resolves
 * the severity and the fixed Russian sentence, and this list adds nothing but a short title so a
 * queue can be scanned. A model that phrased its own complaint would phrase it differently every
 * run, and nobody could ever count how many exercises share a defect.
 *
 * A code outside the seven is printed as the code itself — never blank, never guessed into the
 * nearest known label.
 *
 * Blocking findings sort first: two of the seven mean the exercise actively teaches the wrong thing,
 * and the whole reason the severity split exists is that a list of advisory notes must not bury them.
 */

const BLOCKING_SEVERITY = "blocking";

interface FindingListProps {
    findings: readonly ContentReviewFinding[];
}

export function FindingList({ findings }: FindingListProps) {
    const sortedFindings = useMemo(() => {
        return [...findings].sort((left, right) => {
            const leftIsBlocking = left.severity === BLOCKING_SEVERITY;
            const rightIsBlocking = right.severity === BLOCKING_SEVERITY;
            if (leftIsBlocking !== rightIsBlocking) return leftIsBlocking ? -1 : 1;
            return left.code.localeCompare(right.code);
        });
    }, [findings]);

    if (sortedFindings.length === 0) {
        return (
            <EmptyState
                icon="check"
                compact
                title="Замечаний нет"
                description="Модель прочитала упражнение и не нашла, к чему придраться. Это ожидаемый ответ, а не сбой."
            />
        );
    }

    return (
        <ul className="flex flex-col gap-4">
            {sortedFindings.map((finding, findingIndex) => (
                <li key={`${finding.code}-${findingIndex}`} className="flex flex-col gap-1.5">
                    <div className="flex flex-wrap items-center gap-2">
                        <Chip tone={findingSeverityTone(finding.severity)} size="sm">
                            {describeFindingSeverity(finding.severity)}
                        </Chip>
                        <span className="text-sm font-medium text-ink">
                            {describeFindingCode(finding.code)}
                        </span>
                    </div>
                    <p className="text-sm text-ink-2">{finding.message}</p>
                    {finding.detail && (
                        <p
                            className="text-xs text-ink-3 rounded-lg px-3 py-2"
                            style={{ fontFamily: "var(--font-mono)", background: "var(--bg-2)" }}
                        >
                            {finding.detail}
                        </p>
                    )}
                </li>
            ))}
        </ul>
    );
}
