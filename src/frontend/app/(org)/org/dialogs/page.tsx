"use client";

import { useMemo, useState } from "react";
import { Button } from "@/shared/components/button";
import { EmptyState } from "@/shared/components/empty-state";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { SkeletonList } from "@/shared/components/skeleton";
import { useTeamMemberNames } from "@/features/org-shell/hooks/use-team-directory";
import {
    DialogSessionFilters,
    type DialogScenarioOption,
    type DialogSessionFilterSelection,
} from "@/features/org-dialogs/components/dialog-session-filters";
import { DialogSessionList } from "@/features/org-dialogs/components/dialog-session-list";
import { useDialogSessions } from "@/features/org-dialogs/hooks/use-dialog-sessions";
import { DEFAULT_PANEL_SCORE_CEILING } from "@/features/org-dialogs/lib/dialog-score";
import {
    DEFAULT_DIALOG_SESSION_LIMIT,
    DIALOG_SESSION_PAGE_SIZE,
    isDialogSessionLimitAtMaximum,
    MAXIMUM_DIALOG_SESSION_PAGE_SIZE,
    nextDialogSessionLimit,
} from "@/features/org-dialogs/lib/dialog-session-query";
import { buildMemberNamesByUserId } from "@/features/org-dialogs/lib/team-member-labels";

const SKELETON_ROW_COUNT = 6;

const UNFILTERED_SELECTION: DialogSessionFilterSelection = {
    userId: null,
    modeId: null,
    maximumPanelScore: null,
};

const INITIAL_SELECTION: DialogSessionFilterSelection = {
    ...UNFILTERED_SELECTION,
    maximumPanelScore: DEFAULT_PANEL_SCORE_CEILING,
};

function hasAnyFilter(selection: DialogSessionFilterSelection): boolean {
    return (
        selection.userId !== null ||
        selection.modeId !== null ||
        selection.maximumPanelScore !== null
    );
}

/**
 * O5 «Разговоры команды» (docs/TENANCY/ADMIN_UI_DESIGN.md O5).
 *
 * The screen exists so that a РОП can find three lines for Monday's meeting, which is why it opens
 * already filtered to the conversations that went badly rather than on everything the team ever
 * said. Names come from the team directory the panel shares — ai-service holds no user replica and
 * the summary DTO deliberately carries only a `userId`.
 */
export default function OrganizationDialogsPage() {
    const [appliedSelection, setAppliedSelection] =
        useState<DialogSessionFilterSelection>(INITIAL_SELECTION);
    const [limit, setLimit] = useState(DEFAULT_DIALOG_SESSION_LIMIT);

    // Remounting the filter form is how a reset from outside it — the empty state's «Показать все
    // разговоры» — throws away the draft the person left in it.
    const [filtersInstanceKey, setFiltersInstanceKey] = useState(0);

    // The scenario the list is narrowed to, kept whole rather than as an id: once the filter is
    // applied every row on screen is that scenario, so a selector rebuilt from the rows would
    // otherwise be a control offering only the value it already has.
    const [appliedScenario, setAppliedScenario] = useState<DialogScenarioOption | null>(null);

    const sessionsQuery = useDialogSessions({ ...appliedSelection, limit });
    const { memberNames } = useTeamMemberNames();

    const memberNamesByUserId = useMemo(
        () => buildMemberNamesByUserId(memberNames),
        [memberNames]
    );

    const sessions = useMemo(() => sessionsQuery.data ?? [], [sessionsQuery.data]);

    // Distinct over the rows that came back: no endpoint lists the dialog modes one organization
    // uses, so the answer is the one the data already gave (ADMIN_UI_DESIGN.md O5).
    const scenarioOptions = useMemo<DialogScenarioOption[]>(() => {
        const titlesByModeId = new Map<string, string>();
        if (appliedScenario) titlesByModeId.set(appliedScenario.modeId, appliedScenario.title);
        for (const session of sessions) {
            if (session.modeTitle) titlesByModeId.set(session.modeId, session.modeTitle);
        }
        return Array.from(titlesByModeId.entries())
            .map(([modeId, title]) => ({ modeId, title }))
            .sort((left, right) => left.title.localeCompare(right.title, "ru"));
    }, [sessions, appliedScenario]);

    const applySelection = (selection: DialogSessionFilterSelection) => {
        setAppliedSelection(selection);
        setAppliedScenario(
            scenarioOptions.find((scenario) => scenario.modeId === selection.modeId) ?? null
        );
        setLimit(DEFAULT_DIALOG_SESSION_LIMIT);
    };

    const resetFilters = () => {
        applySelection(UNFILTERED_SELECTION);
        setFiltersInstanceKey((previousKey) => previousKey + 1);
    };

    const isFiltered = hasAnyFilter(appliedSelection);

    return (
        <>
            <PageHeader
                title="Разговоры команды"
                subtitle="Оценённые разговоры менеджеров, новые сверху. Откройте разговор, чтобы процитировать фрагмент."
            />

            <DialogSessionFilters
                key={filtersInstanceKey}
                memberOptions={memberNames.map((member) => ({
                    userId: member.userId,
                    displayName: member.displayName,
                }))}
                scenarioOptions={scenarioOptions}
                initialSelection={appliedSelection}
                onApply={applySelection}
                isRefreshing={sessionsQuery.isFetching}
            />

            {sessionsQuery.isLoading && (
                <SkeletonList count={SKELETON_ROW_COUNT} rowHeight={64} />
            )}

            {!sessionsQuery.isLoading && sessionsQuery.isError && (
                <ErrorState
                    title="Не удалось получить разговоры"
                    message="Разговоры хранит отдельный сервис — остальная панель может при этом работать. Попробуйте ещё раз."
                    onRetry={() => sessionsQuery.refetch()}
                />
            )}

            {!sessionsQuery.isLoading && !sessionsQuery.isError && sessions.length === 0 && (
                <EmptyState
                    icon="message"
                    title={
                        isFiltered
                            ? "Под фильтр не попал ни один разговор"
                            : "Команда ещё не провела ни одного оценённого разговора"
                    }
                    description={
                        isFiltered
                            ? "Попробуйте поднять порог оценки или снять фильтр по менеджеру и сценарию."
                            : "Разговоры появятся здесь после первой практики в тренажёре — оценка выставляется в конце разговора."
                    }
                    action={
                        isFiltered ? (
                            <Button variant="secondary" onClick={resetFilters}>
                                Показать все разговоры
                            </Button>
                        ) : undefined
                    }
                />
            )}

            {!sessionsQuery.isLoading && !sessionsQuery.isError && sessions.length > 0 && (
                <>
                    <DialogSessionList
                        sessions={sessions}
                        memberNamesByUserId={memberNamesByUserId}
                    />

                    <div className="mt-6 flex justify-center">
                        {isDialogSessionLimitAtMaximum(limit) ? (
                            <p className="text-sm text-ink-3 text-center">
                                Показаны первые {MAXIMUM_DIALOG_SESSION_PAGE_SIZE}. Сузьте фильтр —
                                по менеджеру или по оценке.
                            </p>
                        ) : (
                            sessions.length >= limit && (
                                <Button
                                    variant="secondary"
                                    loading={sessionsQuery.isFetching}
                                    onClick={() => setLimit(nextDialogSessionLimit(limit))}
                                >
                                    Показать ещё {DIALOG_SESSION_PAGE_SIZE}
                                </Button>
                            )
                        )}
                    </div>
                </>
            )}
        </>
    );
}
