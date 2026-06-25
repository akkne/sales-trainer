"use client";

import { Icon } from "@/shared/components/icon";
import { Card } from "@/shared/components/card";
import { Progress } from "@/shared/components/progress";
import { StatTile } from "@/shared/components/stat-tile";
import { useDailyQuote } from "@/features/layout/hooks/use-daily-quote";
import { useProfile } from "@/features/profile/hooks/use-profile";

const FALLBACK_QUOTE = {
    text: "Когда клиент говорит «дорого», не называйте скидку. Спросите — «дорого по сравнению с чем?»",
    author: "Skeptic Sergey",
};

interface StatsWidgetProps {
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
    dailyXpGoal: number;
    dailyXpCurrent: number;
}

export function StatsWidget({
    currentStreakDayCount,
    totalXpAmount,
    weeklyXpAmount,
    dailyXpGoal,
    dailyXpCurrent,
}: StatsWidgetProps) {
    const safeGoal = Number.isFinite(dailyXpGoal) ? dailyXpGoal : 0;
    const safeCurrent = Number.isFinite(dailyXpCurrent) ? dailyXpCurrent : 0;
    const remaining = Math.max(0, safeGoal - safeCurrent);
    const { data: dailyQuote } = useDailyQuote();
    const quoteText = dailyQuote?.text ?? FALLBACK_QUOTE.text;
    const quoteAuthor = dailyQuote?.author ?? FALLBACK_QUOTE.author;
    const { data: profileStats } = useProfile();
    const accuracyPercent = Math.round(profileStats?.averageExerciseScore ?? 0);

    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {/* Stats grid 2x2 */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <StatTile
                    label="Стрик"
                    value={currentStreakDayCount}
                    unit="дн"
                    icon={<Icon name="flame" size="xs" />}
                    tone="flame"
                />
                <StatTile
                    label="Неделя"
                    value={weeklyXpAmount}
                    unit="XP"
                    icon={<Icon name="bolt" size="xs" />}
                    tone="primary"
                />
                <StatTile
                    label="Всего"
                    value={totalXpAmount.toLocaleString("ru")}
                    unit="XP"
                    icon={<Icon name="sparkle" size="xs" />}
                    tone="violet"
                />
                <StatTile
                    label="Точность"
                    value={accuracyPercent}
                    unit="%"
                    icon={<Icon name="target" size="xs" />}
                    tone="success"
                />
            </div>

            {/* Daily goal card */}
            <Card padding={20}>
                <div className="eyebrow muted">Сегодня</div>
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "baseline",
                        margin: "8px 0 10px",
                    }}
                >
                    <span
                        style={{
                            fontWeight: 700,
                            fontSize: 21,
                            letterSpacing: "-0.01em",
                        }}
                    >
                        ещё {remaining} XP
                    </span>
                    <span className="tnum" style={{ fontSize: 13, color: "var(--ink-3)" }}>
                        {safeCurrent} / {safeGoal}
                    </span>
                </div>
                <Progress value={safeCurrent} max={safeGoal} tone="indigo" height={10} />
            </Card>

            {/* Tip card */}
            <Card padding={20} style={{ background: "var(--surface-2)" }}>
                <div className="eyebrow muted">Совет дня</div>
                <div
                    style={{
                        fontSize: 14,
                        lineHeight: 1.55,
                        color: "var(--ink-2)",
                        margin: "10px 0 12px",
                    }}
                >
                    {quoteText}
                </div>
                {quoteAuthor && (
                    <div style={{ fontSize: 13, color: "var(--ink-3)" }}>
                        — {quoteAuthor}
                    </div>
                )}
            </Card>

        </div>
    );
}
