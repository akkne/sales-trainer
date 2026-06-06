"use client";

import Link from "next/link";
import { Icon } from "@/shared/components/icon";
import { Card } from "@/shared/components/card";
import { Progress } from "@/shared/components/progress";
import { StatTile } from "@/shared/components/stat-tile";

interface StatsWidgetProps {
    currentStreakDayCount: number;
    totalXpAmount: number;
    weeklyXpAmount: number;
    dailyXpGoal?: number;
    dailyXpCurrent?: number;
}

export function StatsWidget({
    currentStreakDayCount,
    totalXpAmount,
    weeklyXpAmount,
    dailyXpGoal = 100,
    dailyXpCurrent = 40,
}: StatsWidgetProps) {
    const remaining = Math.max(0, dailyXpGoal - dailyXpCurrent);

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
                    value={87}
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
                        {dailyXpCurrent} / {dailyXpGoal}
                    </span>
                </div>
                <Progress value={dailyXpCurrent} max={dailyXpGoal} tone="indigo" height={10} />
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
                    Когда клиент говорит «дорого», не называйте скидку. Спросите —
                    <span style={{ color: "var(--primary)", fontWeight: 600 }}> «дорого по сравнению с чем?»</span>
                </div>
                <div style={{ fontSize: 13, color: "var(--ink-3)" }}>
                    — Skeptic Sergey
                </div>
            </Card>

            {/* League card */}
            <Link href="/league" style={{ textDecoration: "none", color: "inherit" }}>
                <Card padding={20} hover>
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                        }}
                    >
                        <div className="eyebrow muted">Лига</div>
                        <span
                            className="badge"
                            style={{ background: "var(--amber-soft)", color: "var(--amber)" }}
                        >
                            Серебро
                        </span>
                    </div>
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            marginTop: 10,
                        }}
                    >
                        <span style={{ fontSize: 13, color: "var(--ink-3)" }}>
                            Вы на <b style={{ color: "var(--ink)" }}>4 месте</b> · до повышения{" "}
                            <b style={{ color: "var(--primary)" }}>+120 XP</b>
                        </span>
                        <Icon name="chevron-right" size="sm" color="var(--ink-4)" />
                    </div>
                </Card>
            </Link>
        </div>
    );
}
