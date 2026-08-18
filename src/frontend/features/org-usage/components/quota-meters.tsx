"use client";

import { MetricBar } from "@/shared/components/metric-bar";
import { formatWholeNumberRu, hasConfiguredLimit, resolveMetricTone } from "@/features/org-usage/lib/format-usage";
import type { AiSpendReport } from "@/features/org-usage/hooks/use-ai-usage-report";

interface QuotaMeterDefinition {
    key: string;
    label: string;
    value: number;
    limit: number;
}

interface QuotaMetersProps {
    report: AiSpendReport;
}

/**
 * The three ceilings O17 shows (docs/TENANCY/ADMIN_UI_DESIGN.md §4.3 `QuotaMeters`): the monthly
 * LLM token budget, and the two voice windows (day, month) that sit under it. A limit of `0` means
 * "no ceiling configured" and draws no bar at all, rather than a bar stuck at 0% — the design is
 * explicit that this is a different statement from "you have used none of your limit".
 */
export function QuotaMeters({ report }: QuotaMetersProps) {
    const meters: QuotaMeterDefinition[] = [
        {
            key: "tokens",
            label: "Токены модели",
            value: report.llmTotalTokens,
            limit: report.llmMonthlyTokenLimit,
        },
        {
            key: "voice-today",
            label: "Голос сегодня, мин",
            value: report.voiceUsedMinutesToday,
            limit: report.voiceDailyLimitMinutes,
        },
        {
            key: "voice-month",
            label: "Голос за месяц, мин",
            value: report.voiceUsedMinutesThisMonth,
            limit: report.voiceMonthlyLimitMinutes,
        },
    ];

    return (
        <div className="flex flex-col gap-4">
            {meters.map((meter) =>
                hasConfiguredLimit(meter.limit) ? (
                    <MetricBar
                        key={meter.key}
                        label={meter.label}
                        value={meter.value}
                        limit={meter.limit}
                        tone={resolveMetricTone(meter.value, meter.limit)}
                        formatter={formatWholeNumberRu}
                    />
                ) : (
                    <div key={meter.key} className="flex items-baseline justify-between gap-3">
                        <span className="text-xs text-ink-3">{meter.label}</span>
                        <span className="tnum text-xs font-semibold text-ink-2">без лимита</span>
                    </div>
                )
            )}
        </div>
    );
}
