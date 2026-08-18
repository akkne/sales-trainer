"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Modal } from "@/shared/components/modal";
import { Textarea } from "@/shared/components/input";
import { ApiError } from "@/shared/api/api-client";
import { SkillGapCard } from "@/features/org-team/components/skill-gap-card";
import { SuppressedGapCard } from "@/features/org-team/components/suppressed-gap-card";
import {
    useDismissTeamSkillGap,
    useRestoreTeamSkillGap,
    useStartGapContentGeneration,
    type TeamSkillGap,
    type TeamSkillGaps,
} from "@/features/org-team/hooks/use-team-skill-gaps";
import {
    ATTEMPT_PLURAL_FORMS,
    MANAGER_PLURAL_FORMS,
    formatCountWithNoun,
} from "@/features/org-team/utils/team-summary";

const STALE_MEASUREMENT_HINT =
    "Окно сдвинулось между отрисовкой и нажатием — этап больше не проваливается. Обновите страницу.";
const GENERATION_FAILURE_HINT = "Не удалось запустить прогон. Попробуйте ещё раз.";
const RESTORE_FAILURE_HINT = "Не удалось вернуть предложение. Попробуйте ещё раз.";
const CONFLICT_STATUS = 409;

function describeMutationFailure(error: unknown, fallbackMessage: string): string {
    if (error instanceof ApiError) {
        if (error.status === CONFLICT_STATUS) return STALE_MEASUREMENT_HINT;
        if (typeof error.payload.message === "string") return error.payload.message;
    }
    return fallbackMessage;
}

interface SkillGapPanelProps {
    skillGaps: TeamSkillGaps;
    windowDays: number;
}

/// «Что сделать дальше» — the half of O1 that turns a measurement into a press.
///
/// Generation is not a second progress bar: the answer is a run in `structuring`, and the screen
/// leaves for the checkpoint immediately. A run that already exists for the stage comes back
/// unchanged, so pressing twice lands in the same place rather than buying a second lesson.
export function SkillGapPanel({ skillGaps, windowDays }: SkillGapPanelProps) {
    const router = useRouter();
    const [activeStageKey, setActiveStageKey] = useState<string | null>(null);
    const [gapPendingDismissal, setGapPendingDismissal] = useState<TeamSkillGap | null>(null);
    const [dismissalNote, setDismissalNote] = useState("");

    const startGeneration = useStartGapContentGeneration();
    const dismissGap = useDismissTeamSkillGap(windowDays);
    const restoreGap = useRestoreTeamSkillGap(windowDays);

    const handleGenerate = (stageKey: string) => {
        setActiveStageKey(stageKey);
        startGeneration.mutate(stageKey, {
            onSuccess: (job) => router.push(`/org/content/generation/${job.id}`),
        });
    };

    const handleRestore = (stageKey: string) => {
        setActiveStageKey(stageKey);
        restoreGap.mutate(stageKey);
    };

    const closeDismissalDialog = () => {
        setGapPendingDismissal(null);
        setDismissalNote("");
        dismissGap.reset();
    };

    const handleConfirmDismissal = () => {
        if (!gapPendingDismissal) return;
        dismissGap.mutate(
            { stageKey: gapPendingDismissal.stageKey, note: dismissalNote.trim() || undefined },
            { onSuccess: closeDismissalDialog }
        );
    };

    const hasNothingToShow = skillGaps.gaps.length === 0 && skillGaps.suppressed.length === 0;

    return (
        <section aria-labelledby="team-gap-panel-heading" className="mb-8">
            <h2
                id="team-gap-panel-heading"
                className="mb-3 text-xs font-semibold tracking-wide uppercase text-ink-3"
            >
                Что сделать дальше
            </h2>

            {hasNothingToShow ? (
                <Card padding={20}>
                    <p className="text-sm text-ink-2">
                        Ни один этап воронки не проваливается: нет этапа, где было бы от{" "}
                        {formatCountWithNoun(skillGaps.minimumAttemptsForGap, ATTEMPT_PLURAL_FORMS)}
                        , точность {skillGaps.maximumAccuracyPercentForGap}% и ниже и минимум{" "}
                        {formatCountWithNoun(
                            skillGaps.minimumStrugglingManagers,
                            MANAGER_PLURAL_FORMS
                        )}{" "}
                        ниже этой планки.
                    </p>
                </Card>
            ) : (
                <div className="flex flex-col gap-3">
                    {skillGaps.gaps.map((gap) => (
                        <SkillGapCard
                            key={gap.stageKey}
                            gap={gap}
                            maximumAccuracyPercentForGap={skillGaps.maximumAccuracyPercentForGap}
                            onGenerate={handleGenerate}
                            onDismiss={setGapPendingDismissal}
                            isGenerating={
                                startGeneration.isPending && activeStageKey === gap.stageKey
                            }
                            isDismissing={
                                dismissGap.isPending &&
                                gapPendingDismissal?.stageKey === gap.stageKey
                            }
                            generateErrorMessage={
                                startGeneration.isError && activeStageKey === gap.stageKey
                                    ? describeMutationFailure(
                                          startGeneration.error,
                                          GENERATION_FAILURE_HINT
                                      )
                                    : null
                            }
                        />
                    ))}

                    {skillGaps.suppressed.map((suppressedGap) => (
                        <SuppressedGapCard
                            key={suppressedGap.stageKey}
                            suppressedGap={suppressedGap}
                            onRestore={handleRestore}
                            isRestoring={
                                restoreGap.isPending && activeStageKey === suppressedGap.stageKey
                            }
                            restoreErrorMessage={
                                restoreGap.isError && activeStageKey === suppressedGap.stageKey
                                    ? describeMutationFailure(restoreGap.error, RESTORE_FAILURE_HINT)
                                    : null
                            }
                        />
                    ))}
                </div>
            )}

            {!hasNothingToShow && (
                <p className="mt-3 text-xs text-ink-3">
                    Провалом считаем: от{" "}
                    {formatCountWithNoun(skillGaps.minimumAttemptsForGap, ATTEMPT_PLURAL_FORMS)} на
                    этапе, точность {skillGaps.maximumAccuracyPercentForGap}% и ниже, и минимум{" "}
                    {formatCountWithNoun(skillGaps.minimumStrugglingManagers, MANAGER_PLURAL_FORMS)}{" "}
                    ниже этой планки.
                </p>
            )}

            <Modal
                open={gapPendingDismissal !== null}
                onClose={closeDismissalDialog}
                title="Отложить предложение"
                size="sm"
                footer={
                    <>
                        <Button variant="ghost" onClick={closeDismissalDialog}>
                            Отмена
                        </Button>
                        <Button
                            variant="primary"
                            loading={dismissGap.isPending}
                            onClick={handleConfirmDismissal}
                        >
                            Отложить
                        </Button>
                    </>
                }
            >
                <p className="mb-4 text-sm text-ink-2">
                    Предложение по этапу «{gapPendingDismissal?.stageLabel}» вернётся само, когда
                    отсрочка закончится или когда цифра станет хуже. Вернуть его раньше можно тут
                    же, кнопкой.
                </p>
                <Textarea
                    label="Почему откладываете"
                    hint="Необязательно. Команда этого не видит."
                    rows={3}
                    value={dismissalNote}
                    onChange={(changeEvent) => setDismissalNote(changeEvent.target.value)}
                />
                {dismissGap.isError && (
                    <p className="mt-3 text-sm" style={{ color: "var(--heart)" }} role="alert">
                        {describeMutationFailure(dismissGap.error, STALE_MEASUREMENT_HINT)}
                    </p>
                )}
            </Modal>
        </section>
    );
}
