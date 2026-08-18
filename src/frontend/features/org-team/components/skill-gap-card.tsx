"use client";

import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import {
    MANAGER_PLURAL_FORMS,
    formatCountWithNoun,
    ATTEMPT_PLURAL_FORMS,
} from "@/features/org-team/utils/team-summary";
import type { TeamSkillGap } from "@/features/org-team/hooks/use-team-skill-gaps";

interface SkillGapCardProps {
    gap: TeamSkillGap;
    /// Echoed by the server, not a constant of ours — the sentence «ниже 60%» has to name the
    /// number that actually decided the suggestion.
    maximumAccuracyPercentForGap: number;
    onGenerate: (stageKey: string) => void;
    onDismiss: (gap: TeamSkillGap) => void;
    isGenerating: boolean;
    isDismissing: boolean;
    generateErrorMessage: string | null;
}

/// One failing stage of the funnel and the single thing to do about it.
///
/// The panel sits above the heat map on the same screen (roadmap 40.31): the red cell and the
/// button that does something about it have to be in one field of view, because a separate
/// «предложения» tab is a report about a report.
export function SkillGapCard({
    gap,
    maximumAccuracyPercentForGap,
    onGenerate,
    onDismiss,
    isGenerating,
    isDismissing,
    generateErrorMessage,
}: SkillGapCardProps) {
    return (
        <Card padding={20}>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <h3 className="text-base font-medium text-ink">{gap.stageLabel}</h3>
                <span
                    className="mono text-lg font-semibold"
                    style={{ color: "var(--heart)" }}
                    aria-label={`Точность команды на этапе: ${gap.accuracyPercent} процентов`}
                >
                    {gap.accuracyPercent}%
                </span>
            </div>

            <p className="mt-1 text-sm text-ink-3">
                <span className="mono">
                    {formatCountWithNoun(gap.attemptCount, ATTEMPT_PLURAL_FORMS)}
                </span>
                {" · "}
                <span className="mono">
                    {formatCountWithNoun(gap.strugglingManagerCount, MANAGER_PLURAL_FORMS)} из{" "}
                    {gap.measuredManagerCount}
                </span>{" "}
                ниже {maximumAccuracyPercentForGap}%
            </p>

            {gap.weakestSkills.length > 0 && (
                <p className="mt-2 text-sm text-ink-2">
                    <span className="text-ink-3">Слабее всего: </span>
                    {gap.weakestSkills.map((skill, skillIndex) => (
                        <span key={skill.skillId}>
                            {skillIndex > 0 && <span className="text-ink-4"> · </span>}
                            {skill.title}{" "}
                            <span className="mono text-ink-3">{skill.accuracyPercent}%</span>
                        </span>
                    ))}
                </p>
            )}

            <p className="mt-3 text-sm text-ink-2">
                <span className="text-ink-3">Предложим: </span>
                «{gap.proposedTitle}»
            </p>

            {generateErrorMessage && (
                <p className="mt-3 text-sm" style={{ color: "var(--heart)" }} role="alert">
                    {generateErrorMessage}
                </p>
            )}

            <div className="mt-4 flex flex-wrap items-center gap-3">
                <Button
                    variant="primary"
                    loading={isGenerating}
                    disabled={isDismissing}
                    onClick={() => onGenerate(gap.stageKey)}
                >
                    Сгенерировать упражнения
                </Button>
                <Button
                    variant="ghost"
                    disabled={isGenerating || isDismissing}
                    onClick={() => onDismiss(gap)}
                >
                    Не сейчас
                </Button>
            </div>
        </Card>
    );
}
