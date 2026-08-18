"use client";

import { DataTable, type Column } from "@/shared/components/data-table";
import { EmptyState } from "@/shared/components/empty-state";
import { describeUsageKind } from "@/features/org-usage/constants/usage-dictionary";
import {
    describeCallCount,
    formatModelCost,
    formatWholeNumberRu,
} from "@/features/org-usage/lib/format-usage";
import type { AiSpendModelLine, AiSpendReport } from "@/features/org-usage/hooks/use-ai-usage-report";

interface ModelUsageTableProps {
    models: AiSpendModelLine[];
    currency: AiSpendReport["currency"];
    isLoading?: boolean;
}

function describeUnits(line: AiSpendModelLine): string {
    if (line.kind === "llm") {
        return `${formatWholeNumberRu(line.promptTokens)} + ${formatWholeNumberRu(line.completionTokens)} токенов`;
    }
    return `${formatWholeNumberRu(line.speechCharacters)} символов`;
}

/**
 * Per-model breakdown of the month's spend (docs/TENANCY/ADMIN_UI_DESIGN.md O17 «ПО МОДЕЛЯМ»). An
 * empty organization — no calls yet this month — gets an explanation of the section instead of a
 * bare table, matching every other list in this panel.
 */
export function ModelUsageTable({ models, currency, isLoading = false }: ModelUsageTableProps) {
    const columns: Column<AiSpendModelLine>[] = [
        {
            key: "model",
            header: "Модель",
            render: (line) => <span className="font-medium text-ink">{line.model}</span>,
        },
        {
            key: "kind",
            header: "Тип",
            render: (line) => describeUsageKind(line.kind),
        },
        {
            key: "units",
            header: "Расход",
            render: describeUnits,
        },
        {
            key: "calls",
            header: "Вызовы",
            align: "right",
            render: (line) => describeCallCount(line.callCount),
        },
        {
            key: "cost",
            header: "Стоимость",
            align: "right",
            render: (line) => (
                <span className="tnum" style={{ fontFamily: "var(--font-mono)" }}>
                    {formatModelCost(line.estimatedCost, currency)}
                </span>
            ),
        },
    ];

    return (
        <DataTable
            columns={columns}
            rows={models}
            rowKey={(line) => `${line.model}-${line.kind}`}
            isLoading={isLoading}
            empty={
                <EmptyState
                    icon="zap"
                    title="В этом месяце расхода ещё не было"
                    description="Как только команда начнёт разговоры или запустит генерацию контента, расход по каждой модели появится здесь."
                />
            }
        />
    );
}
