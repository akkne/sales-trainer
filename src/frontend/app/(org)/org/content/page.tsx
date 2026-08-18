"use client";

import { PageHeader } from "@/shared/components/page-header";
import { ContentQueueCard } from "@/features/org-content-generation/components/content-queue-card";
import { useContentHubCounters } from "@/features/org-content-generation/hooks/use-content-hub-counters";
import {
    describeAdaptationsQueue,
    describeOverridesQueue,
    describeOwnLessonsQueue,
} from "@/features/org-content-generation/utils/queue-copy";

/**
 * O9 «Контент» (docs/TENANCY/ADMIN_UI_DESIGN.md §2 O9). One fork in the road so that the sidebar
 * carries one entry instead of three.
 *
 * It reads counts and nothing else — three list endpoints, one per queue, none of them detail. The
 * three destinations are owned by other slices (O10 here, O12 and O14 elsewhere); this page links
 * to them and builds none of them.
 */
export default function OrganizationContentPage() {
    const { counts, isLoading, hasRunCountFailure, hasAdaptationCountFailure, hasOverrideCountFailure } =
        useContentHubCounters();

    return (
        <>
            <PageHeader
                title="Контент"
                subtitle="Три очереди: свои уроки из ваших материалов, массовая правка под ваш продукт и ваши версии уроков из общей библиотеки."
            />

            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <ContentQueueCard
                    title="Собственные уроки"
                    copy={describeOwnLessonsQueue(counts)}
                    actionLabel="Сделать урок из материалов"
                    actionHref="/org/content/generation?new=1"
                    isLoading={isLoading}
                    hasCountFailure={hasRunCountFailure}
                />

                <ContentQueueCard
                    title="Массовая правка"
                    copy={describeAdaptationsQueue(counts)}
                    actionLabel="Переписать этап под свой продукт"
                    actionHref="/org/content/adaptations"
                    isLoading={isLoading}
                    hasCountFailure={hasAdaptationCountFailure}
                />

                <ContentQueueCard
                    title="Свои версии"
                    copy={describeOverridesQueue(counts)}
                    actionLabel="Разобрать очередь"
                    actionHref="/org/content/overrides"
                    isLoading={isLoading}
                    hasCountFailure={hasOverrideCountFailure}
                />
            </div>
        </>
    );
}
