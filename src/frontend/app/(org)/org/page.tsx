"use client";

import { EmptyState } from "@/shared/components/empty-state";
import { PageHeader } from "@/shared/components/page-header";

/**
 * Placeholder for O1 «Команда» — the skill heat map and the gap panel.
 *
 * It exists so that `/org` resolves from the moment the shell lands (slice 0) instead of 404-ing
 * until the team slice ships. Slice 2 replaces this file wholesale; nothing should be built on it.
 */
export default function OrganizationTeamPage() {
    return (
        <>
            <PageHeader
                title="Команда"
                subtitle="Где команда проседает по этапам воронки и что с этим делать."
            />
            <EmptyState
                icon="target"
                title="Экран команды готовится"
                description="Тепловая карта навыков и предложения по слабым этапам появятся здесь. Остальные разделы панели уже доступны в меню слева."
            />
        </>
    );
}
