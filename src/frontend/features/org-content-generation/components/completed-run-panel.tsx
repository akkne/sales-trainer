"use client";

import Link from "next/link";
import { Button } from "@/shared/components/button";
import { Card } from "@/shared/components/card";
import { Icon } from "@/shared/components/icon";
import { describeExerciseCount } from "@/features/org-content-generation/utils/queue-copy";

interface CompletedRunPanelProps {
    jobId: string;
    title: string;
    producedLessonId: string | null;
    producedExerciseCount: number;
    onShowToTeam: () => void;
    isShowToTeamPending: boolean;
    /** True once the un-archive succeeded in this sitting — the list route cannot tell us otherwise. */
    wasShownToTeam: boolean;
    showToTeamErrorMessage: string | null;
}

/**
 * O11 layout (г) — the run produced a lesson.
 *
 * It offers three doors and builds none of them itself. «Открыть урок» goes to the ordinary lesson
 * editor because a generated lesson **is** an ordinary lesson — that is the entire point of
 * docs/CONTENT_PIPELINE.md §5, and a private viewer here would be a second rendering path for the
 * same rows. There is deliberately no per-exercise accept/reject: judging what came out is a
 * different queue (O13) with a different life.
 */
export function CompletedRunPanel({
    jobId,
    title,
    producedLessonId,
    producedExerciseCount,
    onShowToTeam,
    isShowToTeamPending,
    wasShownToTeam,
    showToTeamErrorMessage,
}: CompletedRunPanelProps) {
    return (
        <Card padding={24}>
            <div className="flex items-start gap-3">
                <Icon name="check" size="md" style={{ color: "var(--good)" }} />
                <div className="min-w-0 flex-1">
                    <h2 className="text-base font-bold text-ink">
                        Урок готов: «{title}»{" "}
                        <span className="mono font-normal text-ink-3">
                            · {describeExerciseCount(producedExerciseCount)}
                        </span>
                    </h2>

                    <p className="mt-1 text-sm text-ink-3">
                        {wasShownToTeam
                            ? "Урок показан команде."
                            : "Урок скрыт от команды, пока вы его не проверите."}
                    </p>

                    {producedExerciseCount === 0 && (
                        <p className="mt-2 text-sm" style={{ color: "var(--bad)" }} role="alert">
                            Ни одно упражнение не прошло проверку. Показывать такой урок команде
                            нечего — начните новый прогон с более подробным материалом.
                        </p>
                    )}

                    <div className="mt-4 flex flex-wrap items-center gap-2">
                        {producedLessonId && (
                            <Link href={`/org/content/lessons/${producedLessonId}`}>
                                <Button variant="outline" size="md">
                                    Открыть урок
                                </Button>
                            </Link>
                        )}

                        {producedLessonId && !wasShownToTeam && producedExerciseCount > 0 && (
                            <Button
                                variant="outline"
                                size="md"
                                loading={isShowToTeamPending}
                                onClick={onShowToTeam}
                            >
                                Показать команде
                            </Button>
                        )}

                        <Link href={`/org/assignments/new?contentGenerationJobId=${jobId}`}>
                            <Button variant="ghost" size="md">
                                Создать задание по этому уроку
                            </Button>
                        </Link>
                    </div>

                    {showToTeamErrorMessage && (
                        <p className="mt-3 text-xs" style={{ color: "var(--bad)" }} role="alert">
                            {showToTeamErrorMessage}
                        </p>
                    )}
                </div>
            </div>
        </Card>
    );
}
