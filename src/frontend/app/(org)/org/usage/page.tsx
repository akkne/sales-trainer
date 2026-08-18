"use client";

import { Card, CardContent } from "@/shared/components/card";
import { ErrorState } from "@/shared/components/error-state";
import { PageHeader } from "@/shared/components/page-header";
import { Skeleton } from "@/shared/components/skeleton";
import { useAiUsageReport } from "@/features/org-usage/hooks/use-ai-usage-report";
import { QuotaStateBanner } from "@/features/org-usage/components/quota-state-banner";
import { QuotaMeters } from "@/features/org-usage/components/quota-meters";
import { ModelUsageTable } from "@/features/org-usage/components/model-usage-table";
import { describeCallCount, formatTotalCost, formatUsagePeriodLabel, formatWholeNumberRu } from "@/features/org-usage/lib/format-usage";

/**
 * O17 «Расход ИИ» (docs/TENANCY/ADMIN_UI_DESIGN.md §2). Read-only by design: a quota is what the
 * organization bought, and `GET`/`PUT /admin/ai-quota` are `RequirePlatformAdministrator` — a РОП
 * raising their own limit would be a purchase disguised as an administrative action, so this screen
 * never calls those two routes and has no control that would.
 */
export default function OrganizationUsagePage() {
    const { data: report, isLoading, isError, refetch } = useAiUsageReport();

    if (isLoading) {
        return (
            <>
                <PageHeader title="Расход ИИ" subtitle="Загрузка расхода за текущий месяц…" />
                <div className="flex flex-col gap-4">
                    <Skeleton height={96} rounded={16} />
                    <Skeleton height={160} rounded={16} />
                    <Skeleton height={220} rounded={16} />
                </div>
            </>
        );
    }

    if (isError || !report) {
        return (
            <>
                <PageHeader title="Расход ИИ" />
                <ErrorState
                    title="Не удалось загрузить расход"
                    message="Проверьте подключение и попробуйте снова."
                    onRetry={() => refetch()}
                />
            </>
        );
    }

    return (
        <>
            <PageHeader title={`Расход ИИ · ${formatUsagePeriodLabel(report.periodKey)}`} />

            <QuotaStateBanner quotaState={report.quotaState} />

            <Card className="mb-6">
                <CardContent style={{ marginTop: 0 }}>
                    <QuotaMeters report={report} />
                </CardContent>
            </Card>

            <p className="text-sm text-ink-3 mb-6">
                {describeCallCount(report.llmCallCount)} модели · символов синтеза{" "}
                {formatWholeNumberRu(report.speechCharacters)}
            </p>

            <h2 className="text-xs font-medium text-ink-3 uppercase tracking-wide mb-3">По моделям</h2>
            <ModelUsageTable models={report.models} currency={report.currency} />

            <p className="text-sm text-ink-3 mt-4">
                {formatTotalCost(report.estimatedCost, report.hasUnpricedModels, report.currency)}
            </p>
        </>
    );
}
