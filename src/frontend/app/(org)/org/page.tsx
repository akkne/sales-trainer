"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { CardSkeleton } from "@/shared/components/card";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton, SkeletonList } from "@/shared/components/skeleton";
import { useTeamSkillMap } from "@/features/org-shell/hooks/use-team-directory";
import { SkillGapPanel } from "@/features/org-team/components/skill-gap-panel";
import { SkillHeatMap } from "@/features/org-team/components/skill-heat-map";
import { TeamWindowSelector } from "@/features/org-team/components/team-window-selector";
import {
    DEFAULT_TEAM_WINDOW_DAYS,
    type TeamWindowDays,
} from "@/features/org-team/constants/team-window";
import { useOrganizationRoster } from "@/features/org-team/hooks/use-organization-roster";
import { useTeamSkillGaps } from "@/features/org-team/hooks/use-team-skill-gaps";
import type { HeatMapAxis } from "@/features/org-team/utils/heat-map-matrix";
import { mergeTeamRoster } from "@/features/org-team/utils/team-roster";
import {
    ATTEMPT_PLURAL_FORMS,
    PERSON_PLURAL_FORMS,
    formatCountWithNoun,
    formatWindowStartDate,
    summarizeTeamWindow,
} from "@/features/org-team/utils/team-summary";

const HEAT_MAP_SKELETON_ROW_COUNT = 8;

/**
 * O1 «Команда» — the screen the РОП opens weekly (docs/TENANCY/ADMIN_UI_DESIGN.md, O1).
 *
 * It answers two questions in one scroll: where the team sags, and what to do about it right now.
 * The suggestion panel sits above the heat map deliberately — roadmap 40.31 asks for the red cell
 * and the button that acts on it to be in one field of view.
 *
 * One window selector drives both reads. A map drawn over ninety days beside advice computed over
 * thirty would be a contradiction with nothing on screen able to explain it.
 */
export default function OrganizationTeamPage() {
    const [windowDays, setWindowDays] = useState<TeamWindowDays>(DEFAULT_TEAM_WINDOW_DAYS);
    const [heatMapAxis, setHeatMapAxis] = useState<HeatMapAxis>("stages");

    const skillMapQuery = useTeamSkillMap(windowDays);
    const skillGapsQuery = useTeamSkillGaps(windowDays);
    const rosterQuery = useOrganizationRoster();

    const skillMap = skillMapQuery.data;

    const mergedRoster = useMemo(
        () => (skillMap ? mergeTeamRoster(skillMap, rosterQuery.data ?? null) : null),
        [skillMap, rosterQuery.data]
    );

    const subtitle = useMemo(() => {
        if (!skillMap || !mergedRoster) return undefined;
        const summary = summarizeTeamWindow(skillMap, mergedRoster.rows.length);
        return [
            `Данные с ${formatWindowStartDate(skillMap.windowStart)}`,
            formatCountWithNoun(summary.memberCount, PERSON_PLURAL_FORMS),
            formatCountWithNoun(summary.attemptCount, ATTEMPT_PLURAL_FORMS),
        ].join(" · ");
    }, [skillMap, mergedRoster]);

    const isLoading = skillMapQuery.isLoading || skillGapsQuery.isLoading;
    const hasFailed = skillMapQuery.isError || skillGapsQuery.isError;

    const retry = () => {
        void skillMapQuery.refetch();
        void skillGapsQuery.refetch();
        void rosterQuery.refetch();
    };

    if (isLoading) {
        return (
            <>
                <PageHeader title="Команда" />
                <Skeleton height={28} width={280} />
                <div className="mt-6 flex flex-col gap-3">
                    <CardSkeleton lines={3} showHeader={false} />
                    <CardSkeleton lines={2} showHeader={false} />
                </div>
                <div className="mt-8">
                    <SkeletonList count={HEAT_MAP_SKELETON_ROW_COUNT} rowHeight={40} />
                </div>
            </>
        );
    }

    if (hasFailed || !skillMap || !skillGapsQuery.data || !mergedRoster) {
        return (
            <>
                <PageHeader title="Команда" />
                <ErrorState
                    message="Не удалось прочитать данные команды. Карта и предложения считаются по одному окну, поэтому загружаются вместе."
                    onRetry={retry}
                />
            </>
        );
    }

    const hasAnyPractice = skillMap.stages.length > 0;

    return (
        <>
            <PageHeader
                title="Команда"
                subtitle={subtitle}
                action={<TeamWindowSelector value={windowDays} onChange={setWindowDays} />}
            />

            {hasAnyPractice ? (
                <>
                    <SkillGapPanel skillGaps={skillGapsQuery.data} windowDays={windowDays} />
                    <SkillHeatMap
                        skillMap={skillMap}
                        rows={mergedRoster.rows}
                        isRosterKnown={mergedRoster.isRosterKnown}
                        axis={heatMapAxis}
                        onAxisChange={setHeatMapAxis}
                    />
                </>
            ) : (
                <EmptyState
                    icon="target"
                    title="Пока никто из команды не решал упражнения"
                    description={
                        mergedRoster.rows.length > 0
                            ? `Тепловая карта появится после первых попыток — обычно это первая неделя после выдачи задания. В компании ${formatCountWithNoun(mergedRoster.rows.length, PERSON_PLURAL_FORMS)}, и ни у кого пока нет ни одной попытки.`
                            : "Тепловая карта появится после первых попыток — обычно это первая неделя после выдачи задания."
                    }
                    action={
                        <Link href="/org/assignments/new">
                            <Button variant="primary">Создать задание</Button>
                        </Link>
                    }
                />
            )}
        </>
    );
}
